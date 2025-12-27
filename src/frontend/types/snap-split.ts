/** 帳單同步狀態 */
export type SyncStatus = 'local' | 'synced' | 'modified' | 'syncing' | 'error';

export interface Member {
    id: string;
    name: string;
    /** 遠端 ID（同步後設定） */
    remoteId?: string;
}

export interface ExpenseItem {
    id: string;
    name: string;
    amount: number;
    paidBy: string;
    participants: string[];
    /** 遠端 ID（同步後設定） */
    remoteId?: string;
}

export interface Expense {
    id: string;
    name: string;
    serviceFeePercent: number;
    isItemized: boolean;
    amount: number;
    paidBy: string;
    participants: string[];
    items: ExpenseItem[];
    /** 遠端 ID（同步後設定） */
    remoteId?: string;
}

export interface Bill {
    id: string;
    name: string;
    members: Member[];
    expenses: Expense[];
    settledTransfers: string[];
    createdAt: string;
    updatedAt: string;
    /** 同步狀態 */
    syncStatus: SyncStatus;
    /** 遠端帳單 ID（同步後設定） */
    remoteId?: string;
    /** 雲端分享碼 */
    shareCode?: string;
    /** 最後同步時間 */
    lastSyncedAt?: string;
    /** 同步錯誤訊息 */
    syncError?: string;
}

export interface Transfer {
    from: string;
    to: string;
    amount: number;
}

export interface MemberSummary {
    memberId: string;
    totalPaid: number;
    totalOwed: number;
    balance: number;
}

export interface SettlementResult {
    totalAmount: number;
    totalWithServiceFee: number;
    memberSummaries: MemberSummary[];
    transfers: Transfer[];
}
