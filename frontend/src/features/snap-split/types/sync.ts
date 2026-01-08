/**
 * 同步相關類型定義
 * 用於 Delta Sync API 與前端 Store 之間的類型轉換
 */

import type { Bill, Member, Expense } from './snap-split';

/** ID 映射：本地 ID → 遠端 ID */
export interface IdMappings {
    members: Record<string, string>;
    expenses: Record<string, string>;
    expenseItems: Record<string, string>;
}

/** 同步衝突資訊 */
export interface SyncConflict {
    entityType: 'bill' | 'member' | 'expense' | 'expenseItem';
    entityId: string;
    field: string;
    localValue: unknown;
    serverValue: unknown;
    resolvedValue: unknown;
}

/** 同步結果 */
export interface SyncResult {
    success: boolean;
    newVersion?: number;
    idMappings?: IdMappings;
    conflicts?: SyncConflict[];
    mergedBill?: Bill;
    error?: SyncError;
}

/** 同步錯誤類型 */
export type SyncErrorCode =
    | 'NETWORK_ERROR'
    | 'UNAUTHORIZED'
    | 'VERSION_CONFLICT'
    | 'VALIDATION_ERROR'
    | 'SERVER_ERROR'
    | 'UNKNOWN';

/** 同步錯誤 */
export interface SyncError {
    code: SyncErrorCode;
    message: string;
    retryable: boolean;
    details?: Record<string, unknown>;
}

/** 實體變更追蹤 */
export interface EntityChanges<TAdd, TUpdate> {
    add: TAdd[];
    update: TUpdate[];
    delete: string[]; // Remote IDs
}

/** 成員新增 DTO */
export interface MemberAdd {
    localId: string;
    name: string;
    displayOrder: number;
}

/** 成員更新 DTO */
export interface MemberUpdate {
    id: string; // Remote ID
    name?: string;
    displayOrder?: number;
}

/** 費用新增 DTO */
export interface ExpenseAdd {
    localId: string;
    name: string;
    amount: number;
    serviceFeePercent: number;
    isItemized: boolean;
    paidByMemberId: string;
    participantIds: string[];
    items?: ExpenseItemAdd[];
}

/** 費用更新 DTO */
export interface ExpenseUpdate {
    id: string; // Remote ID
    name?: string;
    amount?: number;
    serviceFeePercent?: number;
    paidByMemberId?: string;
    participantIds?: string[];
}

/** 費用項目新增 DTO */
export interface ExpenseItemAdd {
    localId: string;
    name: string;
    amount: number;
    paidByMemberId: string;
    participantIds: string[];
}

/** 費用項目更新 DTO */
export interface ExpenseItemUpdate {
    id: string; // Remote ID
    name?: string;
    amount?: number;
    paidByMemberId?: string;
    participantIds?: string[];
}

/** 結算變更 DTO */
export interface SettlementChanges {
    settled: string[];   // "fromMemberId-toMemberId" 格式
    unsettled: string[];
}

/** 帳單 Meta 變更 DTO */
export interface BillMetaChanges {
    name?: string;
}

/** Delta Sync 請求建構結果 */
export interface DeltaSyncPayload {
    baseVersion: number;
    members?: EntityChanges<MemberAdd, MemberUpdate>;
    expenses?: EntityChanges<ExpenseAdd, ExpenseUpdate>;
    expenseItems?: EntityChanges<ExpenseItemAdd, ExpenseItemUpdate>;
    settlements?: SettlementChanges;
    billMeta?: BillMetaChanges;
}

/** 快照差異 - 用於計算本地變更 */
export interface BillSnapshot {
    name: string;
    members: Member[];
    expenses: Expense[];
    settledTransfers: string[];
    version: number;
}

/** 同步佇列項目 (舊版，保留相容) */
export interface SyncQueueItem {
    billId: string;
    timestamp: number;
    retryCount: number;
    lastError?: SyncError;
}

/** 同步操作類型 */
export type SyncActionType = 'FULL_SYNC' | 'DELTA_SYNC';

/** 同步操作狀態 */
export type SyncActionStatus = 'pending' | 'processing' | 'completed' | 'failed';

/**
 * 同步佇列操作
 * @see SNAPSPLIT_SPEC.md Section 8.2
 */
export interface SyncAction {
    /** 唯一識別碼 */
    id: string;
    /** 操作類型 */
    type: SyncActionType;
    /** 本地帳單 ID */
    billLocalId: string;
    /** 請求 payload (SyncBillRequestDto | DeltaSyncRequest) */
    payload: unknown;
    /** 建立時間 */
    createdAt: string;
    /** 重試次數 */
    retryCount: number;
    /** 最大重試次數 */
    maxRetries: number;
    /** 操作狀態 */
    status: SyncActionStatus;
    /** 錯誤碼 (4xx = 永久錯誤, 5xx = 可重試) */
    errorCode?: string;
    /** 錯誤訊息 */
    errorMessage?: string;
}

/** 網路狀態 */
export type NetworkStatus = 'online' | 'offline' | 'slow';

/** 同步狀態指示器 Props */
export interface SyncStatusIndicatorProps {
    status: 'synced' | 'syncing' | 'modified' | 'conflict' | 'error';
    onRetry?: () => void;
    errorMessage?: string;
}

/** 實體類型（用於 ID 解析錯誤） */
export type SyncEntityType = 'Bill' | 'Member' | 'Expense' | 'ExpenseItem';

/** 缺少 Remote ID 錯誤 */
export class MissingRemoteIdError extends Error {
    constructor(
        public readonly entityType: SyncEntityType,
        public readonly localId: string
    ) {
        super(`Missing remoteId for ${entityType} (localId: ${localId})`);
        this.name = 'MissingRemoteIdError';
        Object.setPrototypeOf(this, MissingRemoteIdError.prototype);
    }
}

/** 驗證 Remote ID 是否有效 */
export function isValidRemoteId(id: string | undefined | null): id is string {
    return typeof id === 'string' && id.trim().length > 0;
}

/** 類型守衛：檢查是否為 MissingRemoteIdError */
export function isMissingRemoteIdError(error: unknown): error is MissingRemoteIdError {
    return error instanceof MissingRemoteIdError;
}
