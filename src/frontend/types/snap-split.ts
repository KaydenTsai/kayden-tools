/** 帳單同步狀態 */
export type SyncStatus = 'local' | 'synced' | 'modified' | 'syncing' | 'error';

/** 協作者角色 */
export type CollaboratorRole = 'owner' | 'collaborator' | 'viewer';

export interface Member {
    id: string;
    name: string;
    /** 遠端 ID（同步後設定） */
    remoteId?: string;
    /** 認領者的 User ID */
    userId?: string;
    /** 認領者的頭像 URL */
    avatarUrl?: string;
    /** 原始名稱（認領前的名稱，用於取消認領時還原） */
    originalName?: string;
    /** 認領時間 */
    claimedAt?: string;
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
    /** 帳單擁有者 User ID */
    ownerId?: string;
    /** 是否為匯入的快照（與原作者斷開連結） */
    isSnapshot?: boolean;
    /** 快照來源（原始 shareCode 或分享者資訊） */
    snapshotSource?: string;
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
