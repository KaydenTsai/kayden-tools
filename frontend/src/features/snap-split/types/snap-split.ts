import type { BillSnapshot } from './sync';

/**
 * 帳單同步狀態
 * @see SNAPSPLIT_SPEC.md Section 8.1
 */
export type SyncStatus =
    | 'local'      // 從未同步過（無 remoteId）
    | 'synced'     // 已同步，無本地變更
    | 'modified'   // 有未同步的本地變更
    | 'syncing'    // 正在同步中
    | 'conflict'   // 發生版本衝突，待解決
    | 'error';     // 同步失敗

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
    /** 建立時間（用於防止重複新增） */
    createdAt?: string;
}

export interface ExpenseItem {
    id: string;
    name: string;
    amount: number;
    /** 付款者 Member ID（可為空） */
    paidById: string;
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
    /** 付款者 Member ID（可為空） */
    paidById: string;
    participants: string[];
    items: ExpenseItem[];
    /** 遠端 ID（同步後設定） */
    remoteId?: string;
    /** 待同步的刪除項目 ID 清單 (優先使用 Remote IDs) */
    deletedItemIds?: string[];
    /** 建立時間（用於防止重複新增） */
    createdAt?: string;
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
    /** 版本號（用於樂觀鎖同步） */
    version: number;
    /** 待同步的刪除項目 ID 清單 (Remote IDs) */
    deletedMemberIds?: string[];
    /** 待同步的刪除項目 ID 清單 (Remote IDs) */
    deletedExpenseIds?: string[];
    /** 待同步的取消結算清單 (from-to 格式) */
    unsettledTransfers?: string[];
    /** 本地修訂版號 (用於同步競態條件檢查) */
    _rev?: number;
    /** 同步錯誤訊息 */
    syncError?: string;
    /** 帳單擁有者 User ID */
    ownerId?: string;
    /** 軟刪除標記 */
    isDeleted: boolean;
    /** 上次成功同步時的狀態快照（用於計算 delta） */
    syncSnapshot?: BillSnapshot;
    /** 刪除時間 */
    deletedAt?: string;
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
