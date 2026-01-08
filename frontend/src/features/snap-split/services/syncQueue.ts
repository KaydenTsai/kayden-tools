/**
 * SyncQueue - 同步佇列服務
 * @see SNAPSPLIT_SPEC.md Section 8.2
 *
 * 負責管理離線優先的同步操作佇列，確保資料最終一致性。
 * 特點：
 * - localStorage 持久化，關閉瀏覽器後可恢復
 * - 指數退避重試策略
 * - 4xx 錯誤標記為永久失敗（需用戶介入）
 * - 5xx/網路錯誤標記為可重試
 */

import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { SyncAction, SyncActionStatus, IdMappings } from '../types/sync';
import { syncBill, deltaSyncBill } from '@/api/endpoints/bills/bills';
import type { SyncBillRequestDto, DeltaSyncRequest } from '@/api/models';

// ============================================================================
// Constants
// ============================================================================

const STORAGE_KEY = 'snap-split-sync-queue';
const MAX_QUEUE_SIZE = 1000;
const BASE_DELAY_MS = 1000;
const MAX_DELAY_MS = 30000;
const MAX_RETRIES = 5;

// ============================================================================
// Types
// ============================================================================

interface SyncQueueState {
    queue: SyncAction[];
    processing: boolean;
    lastProcessedAt: string | null;

    // Actions
    enqueue: (action: Omit<SyncAction, 'id' | 'createdAt' | 'retryCount' | 'status' | 'maxRetries'>) => string;
    process: () => Promise<void>;
    retryAction: (actionId: string) => void;
    discardAction: (actionId: string) => void;
    updateQueueWithIdMappings: (mappings: IdMappings) => void;
    getFailedActions: () => SyncAction[];
    getPendingActions: () => SyncAction[];
    rehydrate: () => void;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * 生成唯一 ID
 */
function generateId(): string {
    return crypto.randomUUID();
}

/**
 * 計算指數退避延遲
 */
function calculateBackoffDelay(retryCount: number): number {
    const delay = BASE_DELAY_MS * Math.pow(2, retryCount);
    return Math.min(delay, MAX_DELAY_MS);
}

/**
 * 判斷錯誤是否可重試
 */
function isRetryableError(error: unknown): boolean {
    // 網路錯誤
    if (error instanceof TypeError && error.message.includes('fetch')) {
        return true;
    }

    // Axios 錯誤
    if (typeof error === 'object' && error !== null) {
        const axiosError = error as { response?: { status?: number }; code?: string };

        // 網路斷線
        if (axiosError.code === 'ERR_NETWORK' || axiosError.code === 'ECONNABORTED') {
            return true;
        }

        // 5xx 伺服器錯誤可重試
        const status = axiosError.response?.status;
        if (status && status >= 500 && status < 600) {
            return true;
        }

        // 4xx 客戶端錯誤不可重試
        if (status && status >= 400 && status < 500) {
            return false;
        }
    }

    // 預設可重試
    return true;
}

/**
 * 從錯誤中提取訊息和代碼
 */
function extractErrorInfo(error: unknown): { code: string; message: string } {
    if (typeof error === 'object' && error !== null) {
        const axiosError = error as {
            response?: { status?: number; data?: { error?: { message?: string } } };
            message?: string;
            code?: string;
        };

        const status = axiosError.response?.status;
        const serverMessage = axiosError.response?.data?.error?.message;

        if (status) {
            return {
                code: String(status),
                message: serverMessage || axiosError.message || `HTTP ${status} Error`,
            };
        }

        if (axiosError.code) {
            return {
                code: axiosError.code,
                message: axiosError.message || 'Network error',
            };
        }
    }

    if (error instanceof Error) {
        return {
            code: 'UNKNOWN',
            message: error.message,
        };
    }

    return {
        code: 'UNKNOWN',
        message: String(error),
    };
}

/**
 * Deep replace IDs in an object
 * 用於將 payload 中的 localId 替換為 remoteId
 */
export function deepReplaceIds(obj: unknown, mappings: Record<string, string>): unknown {
    if (obj === null || obj === undefined) {
        return obj;
    }

    if (typeof obj === 'string') {
        return mappings[obj] ?? obj;
    }

    if (Array.isArray(obj)) {
        return obj.map(item => deepReplaceIds(item, mappings));
    }

    if (typeof obj === 'object') {
        const result: Record<string, unknown> = {};
        for (const [key, value] of Object.entries(obj as Record<string, unknown>)) {
            result[key] = deepReplaceIds(value, mappings);
        }
        return result;
    }

    return obj;
}

// ============================================================================
// Store
// ============================================================================

export const useSyncQueue = create<SyncQueueState>()(
    subscribeWithSelector((set, get) => ({
        queue: [],
        processing: false,
        lastProcessedAt: null,

        /**
         * 將同步操作加入佇列
         */
        enqueue: (action) => {
            const id = generateId();
            const now = new Date().toISOString();

            const newAction: SyncAction = {
                ...action,
                id,
                createdAt: now,
                retryCount: 0,
                maxRetries: MAX_RETRIES,
                status: 'pending',
            };

            set((state) => {
                // 檢查佇列大小限制
                if (state.queue.length >= MAX_QUEUE_SIZE) {
                    console.warn('[SyncQueue] Queue is full, dropping oldest completed actions');
                    const filteredQueue = state.queue.filter(a => a.status !== 'completed');
                    return {
                        queue: [...filteredQueue, newAction].slice(-MAX_QUEUE_SIZE),
                    };
                }

                return {
                    queue: [...state.queue, newAction],
                };
            });

            // 持久化
            persist(get().queue);

            // 觸發處理
            void get().process();

            return id;
        },

        /**
         * 處理佇列中的同步操作
         */
        process: async () => {
            const state = get();

            // 已在處理中，跳過
            if (state.processing) {
                return;
            }

            // 取得待處理的操作
            const pendingAction = state.queue.find(a => a.status === 'pending');
            if (!pendingAction) {
                return;
            }

            // 標記為處理中
            set({ processing: true });
            updateActionStatus(pendingAction.id, 'processing');

            try {
                // 執行同步
                await executeSyncAction(pendingAction);

                // 成功
                updateActionStatus(pendingAction.id, 'completed');

            } catch (error) {
                const { code, message } = extractErrorInfo(error);
                const retryable = isRetryableError(error);

                if (retryable && pendingAction.retryCount < pendingAction.maxRetries) {
                    // 可重試：增加重試次數，設回 pending
                    set((state) => ({
                        queue: state.queue.map(a =>
                            a.id === pendingAction.id
                                ? {
                                    ...a,
                                    status: 'pending' as SyncActionStatus,
                                    retryCount: a.retryCount + 1,
                                    errorCode: code,
                                    errorMessage: message,
                                }
                                : a
                        ),
                    }));

                    // 延遲後重試
                    const delay = calculateBackoffDelay(pendingAction.retryCount);
                    setTimeout(() => void get().process(), delay);
                } else {
                    // 不可重試或已達上限：標記為失敗
                    set((state) => ({
                        queue: state.queue.map(a =>
                            a.id === pendingAction.id
                                ? {
                                    ...a,
                                    status: 'failed' as SyncActionStatus,
                                    errorCode: code,
                                    errorMessage: message,
                                }
                                : a
                        ),
                    }));
                }

                persist(get().queue);
            }

            // 完成處理
            set({
                processing: false,
                lastProcessedAt: new Date().toISOString(),
            });
            persist(get().queue);

            // 繼續處理下一個
            void get().process();
        },

        /**
         * 重試失敗的操作
         */
        retryAction: (actionId) => {
            set((state) => ({
                queue: state.queue.map(a =>
                    a.id === actionId && a.status === 'failed'
                        ? { ...a, status: 'pending' as SyncActionStatus, retryCount: 0 }
                        : a
                ),
            }));
            persist(get().queue);
            void get().process();
        },

        /**
         * 捨棄失敗的操作
         */
        discardAction: (actionId) => {
            set((state) => ({
                queue: state.queue.filter(a => a.id !== actionId),
            }));
            persist(get().queue);
        },

        /**
         * 收到 ID 映射後，更新佇列中待處理操作的 ID
         */
        updateQueueWithIdMappings: (mappings) => {
            const allMappings: Record<string, string> = {
                ...mappings.members,
                ...mappings.expenses,
                ...mappings.expenseItems,
            };

            if (Object.keys(allMappings).length === 0) {
                return;
            }

            set((state) => ({
                queue: state.queue.map(action => {
                    // 只更新 pending 狀態的操作
                    if (action.status !== 'pending') {
                        return action;
                    }

                    // 深度替換 payload 中的 ID
                    const updatedPayload = deepReplaceIds(action.payload, allMappings);
                    return {
                        ...action,
                        payload: updatedPayload,
                    };
                }),
            }));
            persist(get().queue);
        },

        /**
         * 取得所有失敗的操作
         */
        getFailedActions: () => {
            return get().queue.filter(a => a.status === 'failed');
        },

        /**
         * 取得所有待處理的操作
         */
        getPendingActions: () => {
            return get().queue.filter(a => a.status === 'pending' || a.status === 'processing');
        },

        /**
         * 從 localStorage 恢復佇列
         */
        rehydrate: () => {
            try {
                const stored = localStorage.getItem(STORAGE_KEY);
                if (!stored) {
                    return;
                }

                const parsed = JSON.parse(stored) as SyncAction[];

                // 過濾掉已完成的操作，恢復其他狀態
                const restored = parsed
                    .filter(a => a.status !== 'completed')
                    .map(a => ({
                        ...a,
                        // 將 processing 狀態重設為 pending（因為重啟後需要重新處理）
                        status: a.status === 'processing' ? 'pending' as SyncActionStatus : a.status,
                    }));

                if (restored.length > 0) {
                    set({ queue: restored });

                    // 自動開始處理
                    void get().process();
                }
            } catch (error) {
                console.error('[SyncQueue] Failed to rehydrate:', error);
            }
        },
    }))
);

// ============================================================================
// Private Helpers
// ============================================================================

/**
 * 持久化佇列到 localStorage
 */
function persist(queue: SyncAction[]): void {
    try {
        // 只保存非完成的操作
        const toPersist = queue.filter(a => a.status !== 'completed');
        localStorage.setItem(STORAGE_KEY, JSON.stringify(toPersist));
    } catch (error) {
        console.error('[SyncQueue] Failed to persist:', error);
    }
}

/**
 * 更新操作狀態
 */
function updateActionStatus(actionId: string, status: SyncActionStatus): void {
    useSyncQueue.setState((state) => ({
        queue: state.queue.map(a =>
            a.id === actionId ? { ...a, status } : a
        ),
    }));
    persist(useSyncQueue.getState().queue);
}

/**
 * 執行同步操作
 */
async function executeSyncAction(action: SyncAction): Promise<void> {
    switch (action.type) {
        case 'FULL_SYNC': {
            const payload = action.payload as SyncBillRequestDto;
            await syncBill(payload);
            break;
        }
        case 'DELTA_SYNC': {
            const payload = action.payload as { billRemoteId: string; request: DeltaSyncRequest };
            await deltaSyncBill(payload.billRemoteId, payload.request);
            break;
        }
        default:
            throw new Error(`Unknown sync action type: ${action.type}`);
    }
}

// ============================================================================
// Initialization
// ============================================================================

/**
 * 初始化 SyncQueue
 * 應在 App 啟動時調用
 */
export function initSyncQueue(): void {
    useSyncQueue.getState().rehydrate();
}

// ============================================================================
// Convenience Exports
// ============================================================================

/**
 * 快捷函式：加入 Full Sync 操作
 */
export function enqueueFullSync(billLocalId: string, payload: SyncBillRequestDto): string {
    return useSyncQueue.getState().enqueue({
        type: 'FULL_SYNC',
        billLocalId,
        payload,
    });
}

/**
 * 快捷函式：加入 Delta Sync 操作
 */
export function enqueueDeltaSync(
    billLocalId: string,
    billRemoteId: string,
    request: DeltaSyncRequest
): string {
    return useSyncQueue.getState().enqueue({
        type: 'DELTA_SYNC',
        billLocalId,
        payload: { billRemoteId, request },
    });
}
