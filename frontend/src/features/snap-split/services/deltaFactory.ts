/**
 * Delta Factory - 從本地 Bill 變更建構 DeltaSyncRequest
 *
 * 核心邏輯：
 * 1. 比較當前 Bill 與上次同步快照（或空白狀態）
 * 2. 識別新增、更新、刪除的實體
 * 3. 建構符合 API 規格的 DeltaSyncRequest
 */

import type { Bill, Member, Expense, ExpenseItem } from '../types/snap-split';
import type { BillSnapshot } from '../types/sync';
import { isValidRemoteId, MissingRemoteIdError } from '../types/sync';
import { parseSettledKey } from '../lib/settlement';
import type {
    DeltaSyncRequest,
    MemberChangesDto,
    ExpenseChangesDto,
    ExpenseItemChangesDto,
    SettlementChangesDto,
    BillMetaChangesDto,
    MemberAddDto,
    MemberUpdateDto,
    ExpenseAddDto,
    ExpenseUpdateDto,
    ExpenseItemAddDto,
    ExpenseItemUpdateDto,
    DeltaSettlementDto,
} from '@/api/models';

/**
 * 建構 DeltaSyncRequest
 * @param bill 當前本地 Bill
 * @param snapshot 上次同步的快照（用於計算差異），若為新帳單則為 undefined
 */
export function buildDeltaSyncRequest(
    bill: Bill,
    snapshot?: BillSnapshot
): DeltaSyncRequest {
    const request: DeltaSyncRequest = {
        baseVersion: bill.version,
    };

    // 計算 Members 變更
    const memberChanges = buildMemberChanges(bill, snapshot);
    if (hasChanges(memberChanges)) {
        request.members = memberChanges;
    }

    // 計算 Expenses 變更
    const expenseChanges = buildExpenseChanges(bill, snapshot);
    if (hasChanges(expenseChanges)) {
        request.expenses = expenseChanges;
    }

    // 計算 ExpenseItems 變更
    const expenseItemChanges = buildExpenseItemChanges(bill, snapshot);
    if (hasChanges(expenseItemChanges)) {
        request.expenseItems = expenseItemChanges;
    }

    // 計算 Settlements 變更
    const settlementChanges = buildSettlementChanges(bill, snapshot);
    if (settlementChanges.mark?.length || settlementChanges.unmark?.length) {
        request.settlements = settlementChanges;
    }

    // 計算 BillMeta 變更
    const billMetaChanges = buildBillMetaChanges(bill, snapshot);
    if (billMetaChanges.name !== undefined) {
        request.billMeta = billMetaChanges;
    }

    return request;
}

/**
 * 檢查是否有變更
 */
function hasChanges<T extends { add?: unknown[] | null; update?: unknown[] | null; delete?: string[] | null }>(
    changes: T
): boolean {
    return (
        (changes.add?.length ?? 0) > 0 ||
        (changes.update?.length ?? 0) > 0 ||
        (changes.delete?.length ?? 0) > 0
    );
}

/**
 * 建構 Members 變更
 */
function buildMemberChanges(bill: Bill, snapshot?: BillSnapshot): MemberChangesDto {
    const snapshotMembers = snapshot?.members ?? [];
    const snapshotMemberIds = new Set(snapshotMembers.map(m => m.id));

    const add: MemberAddDto[] = [];
    const update: MemberUpdateDto[] = [];

    bill.members.forEach((member, index) => {
        if (!member.remoteId) {
            // 新增：沒有 remoteId 表示是新成員
            add.push({
                localId: member.id,
                name: member.name,
                displayOrder: index,
                // 新增時已認領則傳送認領資訊
                linkedUserId: member.userId ?? undefined,
                claimedAt: member.claimedAt ?? undefined,
            });
        } else if (snapshotMemberIds.has(member.id)) {
            // 可能有更新：檢查是否有變更
            const snapshotMember = snapshotMembers.find(m => m.id === member.id);
            if (snapshotMember && hasMemberChanged(member, snapshotMember)) {
                update.push({
                    remoteId: member.remoteId,
                    name: member.name !== snapshotMember.name ? member.name : undefined,
                    displayOrder: index,
                    linkedUserId: member.userId ?? null,
                    claimedAt: member.claimedAt ?? null,
                });
            }
        }
    });

    // 刪除：使用 bill.deletedMemberIds（已是 remoteIds）
    const deleteIds = bill.deletedMemberIds ?? [];

    return {
        add: add.length > 0 ? add : undefined,
        update: update.length > 0 ? update : undefined,
        delete: deleteIds.length > 0 ? deleteIds : undefined,
    };
}

function hasMemberChanged(current: Member, snapshot: Member): boolean {
    return (
        current.name !== snapshot.name ||
        current.userId !== snapshot.userId ||
        current.claimedAt !== snapshot.claimedAt
    );
}

/**
 * 建構 Expenses 變更
 */
function buildExpenseChanges(bill: Bill, snapshot?: BillSnapshot): ExpenseChangesDto {
    const snapshotExpenses = snapshot?.expenses ?? [];
    const snapshotExpenseIds = new Set(snapshotExpenses.map(e => e.id));
    const snapshotMembers = snapshot?.members;

    const add: ExpenseAddDto[] = [];
    const update: ExpenseUpdateDto[] = [];

    bill.expenses.forEach(expense => {
        if (!expense.remoteId) {
            // 新增：允許 fallback 到 localId（新成員可能還沒有 remoteId）
            const paidByMemberId = resolveMemberId(expense.paidById, bill.members);
            const participantIds = expense.participants.map(pid => resolveMemberId(pid, bill.members));
            add.push({
                localId: expense.id,
                name: expense.name,
                amount: expense.amount,
                serviceFeePercent: expense.serviceFeePercent,
                isItemized: expense.isItemized,
                paidByMemberId,
                participantIds,
            });
        } else if (snapshotExpenseIds.has(expense.id)) {
            // 更新：使用嚴格解析（所有參照的成員應該都有 remoteId）
            const snapshotExpense = snapshotExpenses.find(e => e.id === expense.id);
            if (snapshotExpense && hasExpenseChanged(expense, snapshotExpense)) {
                // Itemized expenses 的 paidById/participants 在品項層級處理
                // 只有非 itemized 才需要解析這些欄位
                let paidByMemberId: string | undefined;
                let participantIds: string[] | undefined;

                if (!expense.isItemized && expense.paidById) {
                    paidByMemberId = resolveMemberIdStrict(expense.paidById, bill.members, snapshotMembers);
                    participantIds = expense.participants.map(pid =>
                        resolveMemberIdStrict(pid, bill.members, snapshotMembers)
                    );
                }

                update.push({
                    remoteId: expense.remoteId,
                    name: expense.name !== snapshotExpense.name ? expense.name : undefined,
                    amount: expense.amount !== snapshotExpense.amount ? expense.amount : undefined,
                    serviceFeePercent: expense.serviceFeePercent !== snapshotExpense.serviceFeePercent
                        ? expense.serviceFeePercent : undefined,
                    paidByMemberId,
                    participantIds,
                    isItemized: expense.isItemized !== snapshotExpense.isItemized ? expense.isItemized : undefined,
                });
            }
        }
    });

    // 刪除
    const deleteIds = bill.deletedExpenseIds ?? [];

    return {
        add: add.length > 0 ? add : undefined,
        update: update.length > 0 ? update : undefined,
        delete: deleteIds.length > 0 ? deleteIds : undefined,
    };
}

function hasExpenseChanged(current: Expense, snapshot: Expense): boolean {
    return (
        current.name !== snapshot.name ||
        current.amount !== snapshot.amount ||
        current.serviceFeePercent !== snapshot.serviceFeePercent ||
        current.isItemized !== snapshot.isItemized ||
        current.paidById !== snapshot.paidById ||
        !arraysEqual(current.participants, snapshot.participants)
    );
}

/**
 * 建構 ExpenseItems 變更
 */
function buildExpenseItemChanges(bill: Bill, snapshot?: BillSnapshot): ExpenseItemChangesDto {
    const snapshotItems = new Map<string, ExpenseItem>();
    snapshot?.expenses.forEach(e => {
        e.items.forEach(i => snapshotItems.set(i.id, i));
    });
    const snapshotMembers = snapshot?.members;

    const add: ExpenseItemAddDto[] = [];
    const update: ExpenseItemUpdateDto[] = [];
    const deleteIds: string[] = [];

    bill.expenses.forEach(expense => {
        const expenseId = expense.remoteId ?? expense.id;

        expense.items.forEach(item => {
            if (!item.remoteId) {
                // 新增：允許 fallback
                const paidByMemberId = resolveMemberId(item.paidById, bill.members);
                const participantIds = item.participants.map(pid => resolveMemberId(pid, bill.members));
                add.push({
                    localId: item.id,
                    expenseId,
                    name: item.name,
                    amount: item.amount,
                    paidByMemberId,
                    participantIds,
                });
            } else {
                // 更新：使用嚴格解析
                const snapshotItem = snapshotItems.get(item.id);
                if (snapshotItem && hasExpenseItemChanged(item, snapshotItem)) {
                    const paidByMemberId = resolveMemberIdStrict(item.paidById, bill.members, snapshotMembers);
                    const participantIds = item.participants.map(pid =>
                        resolveMemberIdStrict(pid, bill.members, snapshotMembers)
                    );
                    update.push({
                        remoteId: item.remoteId,
                        name: item.name !== snapshotItem.name ? item.name : undefined,
                        amount: item.amount !== snapshotItem.amount ? item.amount : undefined,
                        paidByMemberId,
                        participantIds,
                    });
                }
            }
        });

        // 收集刪除的 items
        if (expense.deletedItemIds) {
            deleteIds.push(...expense.deletedItemIds);
        }
    });

    return {
        add: add.length > 0 ? add : undefined,
        update: update.length > 0 ? update : undefined,
        delete: deleteIds.length > 0 ? deleteIds : undefined,
    };
}

function hasExpenseItemChanged(current: ExpenseItem, snapshot: ExpenseItem): boolean {
    return (
        current.name !== snapshot.name ||
        current.amount !== snapshot.amount ||
        current.paidById !== snapshot.paidById ||
        !arraysEqual(current.participants, snapshot.participants)
    );
}

/**
 * 建構 Settlements 變更
 */
function buildSettlementChanges(bill: Bill, snapshot?: BillSnapshot): SettlementChangesDto {
    const snapshotSettled = new Set(snapshot?.settledTransfers ?? []);
    const currentSettled = new Set(bill.settledTransfers);
    const snapshotMembers = snapshot?.members;

    const mark: DeltaSettlementDto[] = [];
    const unmark: DeltaSettlementDto[] = [];

    // 新增結算：使用嚴格解析（結算只會發生在現有成員之間）
    currentSettled.forEach(key => {
        if (!snapshotSettled.has(key)) {
            const parsed = parseSettledKey(key);
            if (!parsed) return;
            const [fromId, toId] = parsed;
            mark.push({
                fromMemberId: resolveMemberIdStrict(fromId, bill.members, snapshotMembers),
                toMemberId: resolveMemberIdStrict(toId, bill.members, snapshotMembers),
                amount: 0,
            });
        }
    });

    // 取消結算：已經是 remoteIds（從 server 回傳的）
    (bill.unsettledTransfers ?? []).forEach(key => {
        const parsed = parseSettledKey(key);
        if (!parsed) return;
        const [fromId, toId] = parsed;
        unmark.push({
            fromMemberId: fromId,
            toMemberId: toId,
            amount: 0,
        });
    });

    return {
        mark: mark.length > 0 ? mark : undefined,
        unmark: unmark.length > 0 ? unmark : undefined,
    };
}

/**
 * 建構 BillMeta 變更
 */
function buildBillMetaChanges(bill: Bill, snapshot?: BillSnapshot): BillMetaChangesDto {
    if (!snapshot || bill.name !== snapshot.name) {
        return { name: bill.name };
    }
    return {};
}

/**
 * 解析成員 ID（嚴格模式）
 * - 有 remoteId → 使用 remoteId
 * - 無 remoteId 且是新成員 → 使用 localId
 * - 無 remoteId 且應該有 → 拋出錯誤
 */
function resolveMemberIdStrict(
    localId: string,
    members: Member[],
    snapshotMembers?: Member[]
): string {
    const member = members.find(m => m.id === localId);
    if (!member) {
        throw new MissingRemoteIdError('Member', localId);
    }

    if (isValidRemoteId(member.remoteId)) {
        return member.remoteId;
    }

    // 成員沒有 remoteId，檢查是否為新成員
    const wasInSnapshot = snapshotMembers?.some(m => m.id === localId);
    if (!wasInSnapshot) {
        return localId; // 新成員，使用 localId
    }

    // 成員在快照中存在但沒有 remoteId - 這是 bug
    throw new MissingRemoteIdError('Member', localId);
}

/** 解析成員 ID（允許 fallback，用於首次同步） */
function resolveMemberId(localId: string, members: Member[]): string {
    const member = members.find(m => m.id === localId);
    return member?.remoteId ?? localId;
}

/**
 * 比較兩個陣列是否相等
 */
function arraysEqual<T>(a: T[], b: T[]): boolean {
    if (a.length !== b.length) return false;
    const sortedA = [...a].sort();
    const sortedB = [...b].sort();
    return sortedA.every((val, idx) => val === sortedB[idx]);
}

/**
 * 檢查 DeltaSyncRequest 是否為空（沒有任何變更）
 */
export function isDeltaEmpty(request: DeltaSyncRequest): boolean {
    return (
        !request.members &&
        !request.expenses &&
        !request.expenseItems &&
        !request.settlements &&
        !request.billMeta
    );
}

/**
 * 建立帳單快照（用於下次同步時比較）
 */
export function createBillSnapshot(bill: Bill): BillSnapshot {
    return {
        name: bill.name,
        members: bill.members.map(m => ({ ...m })),
        expenses: bill.expenses.map(e => ({
            ...e,
            items: e.items.map(i => ({ ...i })),
        })),
        settledTransfers: [...bill.settledTransfers],
        version: bill.version,
    };
}
