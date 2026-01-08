/**
 * Bill Adapter - 處理本地 Bill 與遠端 BillDto 之間的類型轉換
 */

import type { Bill, Member, Expense, ExpenseItem } from '../types/snap-split';
import type { IdMappings, SyncConflict, BillSnapshot } from '../types/sync';
import { createSettledKey } from '../lib/settlement';
import type {
    BillDto,
    MemberDto,
    ExpenseDto,
    ExpenseItemDto,
    ConflictInfo,
    DeltaIdMappingsDto,
} from '@/api/models';

/**
 * 為 Bill 建立同步快照
 * 注意：這是 billAdapter 專用的內部函數，避免循環依賴
 */
function createSyncSnapshot(bill: Bill): BillSnapshot {
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

/**
 * 將遠端 BillDto 轉換為本地 Bill 格式
 *
 * 重要：此函數會自動建立 syncSnapshot，確保轉換後的帳單可正確執行 delta sync。
 * 這是 BUG-FIX: 缺少 syncSnapshot 會導致無法偵測 UPDATE 操作。
 */
export function billDtoToBill(dto: BillDto, existingBill?: Partial<Bill>): Bill {
    const now = new Date().toISOString();

    const bill: Bill = {
        // 保留本地 ID，或使用遠端 ID 作為新 ID
        id: existingBill?.id ?? dto.id ?? crypto.randomUUID(),
        name: dto.name ?? '',
        members: (dto.members ?? []).map(m => memberDtoToMember(m)),
        expenses: (dto.expenses ?? []).map(e => expenseDtoToExpense(e)),
        settledTransfers: dto.settledTransfers ?? [],
        createdAt: dto.createdAt ?? existingBill?.createdAt ?? now,
        updatedAt: dto.updatedAt ?? now,
        syncStatus: 'synced',
        remoteId: dto.id,
        shareCode: dto.shareCode ?? existingBill?.shareCode,
        lastSyncedAt: now,
        version: dto.version ?? 0,
        isDeleted: false,
    };

    // 建立同步快照，用於下次 delta sync 計算變更
    bill.syncSnapshot = createSyncSnapshot(bill);

    return bill;
}

/**
 * 將遠端 MemberDto 轉換為本地 Member 格式
 *
 * 若成員已認領 (linkedUserId 存在)，優先使用 Server 的 name 欄位
 * （Server 應在認領時將 name 更新為 linkedUserDisplayName）
 */
export function memberDtoToMember(dto: MemberDto): Member {
    // Server 應在認領時更新 name 欄位，這裡直接使用
    const displayName = dto.name ?? '';

    return {
        id: dto.id ?? crypto.randomUUID(),
        name: displayName,
        remoteId: dto.id,
        userId: dto.linkedUserId ?? undefined,
        avatarUrl: dto.linkedUserAvatarUrl ?? undefined,
        originalName: dto.originalName ?? undefined,
        claimedAt: dto.claimedAt ?? undefined,
    };
}

/**
 * 將遠端 ExpenseDto 轉換為本地 Expense 格式
 */
export function expenseDtoToExpense(dto: ExpenseDto): Expense {
    return {
        id: dto.id ?? crypto.randomUUID(),
        name: dto.name ?? '',
        amount: dto.amount ?? 0,
        serviceFeePercent: dto.serviceFeePercent ?? 0,
        isItemized: dto.isItemized ?? false,
        paidById: dto.paidById ?? '',
        participants: dto.participantIds ?? [],
        items: (dto.items ?? []).map(i => expenseItemDtoToExpenseItem(i)),
        remoteId: dto.id,
    };
}

/**
 * 將遠端 ExpenseItemDto 轉換為本地 ExpenseItem 格式
 */
export function expenseItemDtoToExpenseItem(dto: ExpenseItemDto): ExpenseItem {
    return {
        id: dto.id ?? crypto.randomUUID(),
        name: dto.name ?? '',
        amount: dto.amount ?? 0,
        paidById: dto.paidById ?? '',
        participants: dto.participantIds ?? [],
        remoteId: dto.id,
    };
}

/**
 * 從 Server 合併帳單 - 用於衝突解決後重建本地狀態
 * 保留本地 ID 結構，但使用 Server 的資料內容
 *
 * 重要：此函數會自動建立 syncSnapshot，確保 rebase 後的帳單可正確執行 delta sync。
 * 這是 BUG-FIX: 缺少 syncSnapshot 會導致無法偵測 UPDATE 操作。
 */
export function rebaseBillFromServer(localBill: Bill, serverBill: BillDto): Bill {
    const now = new Date().toISOString();

    // 建立 remoteId → localId 的反向映射
    const memberRemoteToLocal = new Map<string, string>();
    const expenseRemoteToLocal = new Map<string, string>();
    const expenseItemRemoteToLocal = new Map<string, string>();

    localBill.members.forEach(m => {
        if (m.remoteId) memberRemoteToLocal.set(m.remoteId, m.id);
    });
    localBill.expenses.forEach(e => {
        if (e.remoteId) expenseRemoteToLocal.set(e.remoteId, e.id);
        e.items.forEach(i => {
            if (i.remoteId) expenseItemRemoteToLocal.set(i.remoteId, i.id);
        });
    });

    // 轉換 members，保留本地 ID
    const members: Member[] = (serverBill.members ?? []).map(m => {
        const localId = m.id ? memberRemoteToLocal.get(m.id) : undefined;
        return {
            id: localId ?? crypto.randomUUID(),
            name: m.name ?? '',
            remoteId: m.id,
            userId: m.linkedUserId ?? undefined,
            avatarUrl: m.linkedUserAvatarUrl ?? undefined,
            originalName: m.originalName ?? undefined,
            claimedAt: m.claimedAt ?? undefined,
        };
    });

    // 建立新的 remoteId → localId 映射（包含新成員）
    const newMemberRemoteToLocal = new Map<string, string>();
    members.forEach(m => {
        if (m.remoteId) newMemberRemoteToLocal.set(m.remoteId, m.id);
    });

    // 轉換 expenses，保留本地 ID，並轉換成員引用
    const expenses: Expense[] = (serverBill.expenses ?? []).map(e => {
        const localId = e.id ? expenseRemoteToLocal.get(e.id) : undefined;

        // 轉換 paidById 和 participantIds 從 remoteId 到 localId
        const paidById = e.paidById ? (newMemberRemoteToLocal.get(e.paidById) ?? e.paidById) : '';
        const participants = (e.participantIds ?? []).map(
            pid => newMemberRemoteToLocal.get(pid) ?? pid
        );

        // 轉換 items
        const items: ExpenseItem[] = (e.items ?? []).map(i => {
            const itemLocalId = i.id ? expenseItemRemoteToLocal.get(i.id) : undefined;
            const itemPaidById = i.paidById ? (newMemberRemoteToLocal.get(i.paidById) ?? i.paidById) : '';
            const itemParticipants = (i.participantIds ?? []).map(
                pid => newMemberRemoteToLocal.get(pid) ?? pid
            );

            return {
                id: itemLocalId ?? crypto.randomUUID(),
                name: i.name ?? '',
                amount: i.amount ?? 0,
                paidById: itemPaidById,
                participants: itemParticipants,
                remoteId: i.id,
            };
        });

        return {
            id: localId ?? crypto.randomUUID(),
            name: e.name ?? '',
            amount: e.amount ?? 0,
            serviceFeePercent: e.serviceFeePercent ?? 0,
            isItemized: e.isItemized ?? false,
            paidById,
            participants,
            items,
            remoteId: e.id,
            // Rebase 後清除待同步的刪除記錄
            deletedItemIds: undefined,
        };
    });

    // 轉換 settledTransfers（從 remoteId 到 localId）
    // Server 格式: "fromRemoteId::toRemoteId:amount"
    const settledTransfers = (serverBill.settledTransfers ?? []).map(transfer => {
        // 使用正則提取 fromId 和 toId（忽略 amount）
        const match = transfer.match(/^([^:]+)::([^:]+)/);
        if (!match) return transfer;
        const [, fromRemote, toRemote] = match;
        const fromLocal = newMemberRemoteToLocal.get(fromRemote) ?? fromRemote;
        const toLocal = newMemberRemoteToLocal.get(toRemote) ?? toRemote;
        return createSettledKey(fromLocal, toLocal);
    });

    const bill: Bill = {
        id: localBill.id,
        name: serverBill.name ?? localBill.name,
        members,
        expenses,
        settledTransfers,
        createdAt: localBill.createdAt,
        updatedAt: serverBill.updatedAt ?? now,
        syncStatus: 'synced',
        remoteId: serverBill.id ?? localBill.remoteId,
        shareCode: serverBill.shareCode ?? localBill.shareCode,
        lastSyncedAt: now,
        version: serverBill.version ?? localBill.version,
        isDeleted: localBill.isDeleted,
        deletedAt: localBill.deletedAt,
        // 清除待同步的刪除記錄
        deletedMemberIds: undefined,
        deletedExpenseIds: undefined,
        unsettledTransfers: undefined,
    };

    // 建立同步快照，用於下次 delta sync 計算變更
    bill.syncSnapshot = createSyncSnapshot(bill);

    return bill;
}

/**
 * 過濾 null 值的 helper
 */
function filterNullValues(obj: { [key: string]: string | null } | null | undefined): Record<string, string> {
    if (!obj) return {};
    return Object.entries(obj).reduce((acc, [key, value]) => {
        if (value !== null) {
            acc[key] = value;
        }
        return acc;
    }, {} as Record<string, string>);
}

/**
 * 將 API 回傳的 ID 映射轉換為內部格式
 */
export function parseIdMappings(dto: DeltaIdMappingsDto | undefined): IdMappings {
    return {
        members: filterNullValues(dto?.members),
        expenses: filterNullValues(dto?.expenses),
        expenseItems: filterNullValues(dto?.expenseItems),
    };
}

/**
 * 將 API 回傳的衝突資訊轉換為內部格式
 */
export function parseConflicts(conflicts: ConflictInfo[] | null | undefined): SyncConflict[] {
    if (!conflicts) return [];

    return conflicts.map(c => ({
        entityType: parseEntityType(c.type),
        entityId: c.entityId ?? '',
        field: c.field ?? '',
        localValue: c.localValue,
        serverValue: c.serverValue,
        resolvedValue: c.resolvedValue,
    }));
}

function parseEntityType(type: string | null | undefined): SyncConflict['entityType'] {
    switch (type?.toLowerCase()) {
        case 'member': return 'member';
        case 'expense': return 'expense';
        case 'expenseitem': return 'expenseItem';
        default: return 'bill';
    }
}

/**
 * 套用 ID 映射到帳單 - 將本地 ID 更新為遠端 ID
 */
export function applyIdMappingsToBill(bill: Bill, mappings: IdMappings): Bill {
    // 建立本地 ID → 遠端 ID 映射
    const memberLocalToRemote = new Map(Object.entries(mappings.members));
    const expenseLocalToRemote = new Map(Object.entries(mappings.expenses));
    const expenseItemLocalToRemote = new Map(Object.entries(mappings.expenseItems));

    // 更新 members
    const members = bill.members.map(m => ({
        ...m,
        remoteId: memberLocalToRemote.get(m.id) ?? m.remoteId,
    }));

    // 更新 expenses 和 items
    const expenses = bill.expenses.map(e => ({
        ...e,
        remoteId: expenseLocalToRemote.get(e.id) ?? e.remoteId,
        items: e.items.map(i => ({
            ...i,
            remoteId: expenseItemLocalToRemote.get(i.id) ?? i.remoteId,
        })),
    }));

    return {
        ...bill,
        members,
        expenses,
    };
}

/**
 * 解析成員 ID - 返回遠端 ID（如果有）或本地 ID
 * 用於建構 API 請求時的 ID 解析
 */
export function resolveRemoteId(
    localId: string,
    entity: { remoteId?: string } | undefined
): string {
    return entity?.remoteId ?? localId;
}

/**
 * 檢查帳單是否有待同步的變更
 */
export function hasUnsyncedChanges(bill: Bill): boolean {
    return (
        bill.syncStatus === 'modified' ||
        bill.syncStatus === 'local' ||
        (bill.deletedMemberIds?.length ?? 0) > 0 ||
        (bill.deletedExpenseIds?.length ?? 0) > 0 ||
        (bill.unsettledTransfers?.length ?? 0) > 0
    );
}
