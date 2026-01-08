import type { Member, Expense, ExpenseItem } from '../types/snap-split';
import { isValidRemoteId, MissingRemoteIdError } from '../types/sync';

/** 嚴格 ID 解析器 - 不允許 fallback 到 localId */
export class StrictIdResolver {
    private memberMap: Map<string, Member>;
    private expenseMap: Map<string, Expense>;
    private itemMap: Map<string, ExpenseItem>;

    constructor(members: Member[], expenses: Expense[]) {
        this.memberMap = new Map(members.map(m => [m.id, m]));
        this.expenseMap = new Map(expenses.map(e => [e.id, e]));
        this.itemMap = new Map();
        expenses.forEach(e => {
            e.items.forEach(i => this.itemMap.set(i.id, i));
        });
    }

    /** 解析成員的 remoteId，若無則拋出錯誤 */
    resolveMemberRemoteId(localId: string): string {
        const member = this.memberMap.get(localId);
        if (!member) {
            throw new MissingRemoteIdError('Member', localId);
        }
        if (!isValidRemoteId(member.remoteId)) {
            throw new MissingRemoteIdError('Member', localId);
        }
        return member.remoteId;
    }

    /** 解析費用的 remoteId，若無則拋出錯誤 */
    resolveExpenseRemoteId(localId: string): string {
        const expense = this.expenseMap.get(localId);
        if (!expense) {
            throw new MissingRemoteIdError('Expense', localId);
        }
        if (!isValidRemoteId(expense.remoteId)) {
            throw new MissingRemoteIdError('Expense', localId);
        }
        return expense.remoteId;
    }

    /** 解析費用項目的 remoteId，若無則拋出錯誤 */
    resolveItemRemoteId(localId: string): string {
        const item = this.itemMap.get(localId);
        if (!item) {
            throw new MissingRemoteIdError('ExpenseItem', localId);
        }
        if (!isValidRemoteId(item.remoteId)) {
            throw new MissingRemoteIdError('ExpenseItem', localId);
        }
        return item.remoteId;
    }

    /** 檢查成員是否已有 remoteId */
    hasMemberRemoteId(localId: string): boolean {
        const member = this.memberMap.get(localId);
        return member ? isValidRemoteId(member.remoteId) : false;
    }

    /** 檢查費用是否已有 remoteId */
    hasExpenseRemoteId(localId: string): boolean {
        const expense = this.expenseMap.get(localId);
        return expense ? isValidRemoteId(expense.remoteId) : false;
    }

    /** 檢查所有成員是否都已同步 */
    areAllMembersSynced(): boolean {
        for (const member of this.memberMap.values()) {
            if (!isValidRemoteId(member.remoteId)) {
                return false;
            }
        }
        return true;
    }

    /** 取得尚未同步的成員 localIds */
    getUnsyncedMemberIds(): string[] {
        const ids: string[] = [];
        for (const member of this.memberMap.values()) {
            if (!isValidRemoteId(member.remoteId)) {
                ids.push(member.id);
            }
        }
        return ids;
    }
}