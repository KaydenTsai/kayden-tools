/**
 * useBillSync - 帳單同步 Hook
 *
 * 核心同步邏輯：
 * 1. 首次同步：使用 syncBill API 上傳完整帳單
 * 2. 增量同步：使用 deltaSyncBill API 傳送差異
 * 3. 衝突處理：使用 Server 回傳的 mergedBill 重建本地狀態
 */

import { useCallback, useState, useRef } from 'react';
import { useSnapSplitStore } from '../stores/snapSplitStore';
import { useSyncBill, useDeltaSyncBill, getBillByShareCode } from '@/api/endpoints/bills/bills';
import { buildDeltaSyncRequest, isDeltaEmpty } from '../services/deltaFactory';
import { parseIdMappings, billDtoToBill } from '../services/billAdapter';
import { useSyncQueue } from '../services/syncQueue';
import type { Bill } from '../types/snap-split';
import type { SyncResult, SyncError } from '../types/sync';
import { isMissingRemoteIdError } from '../types/sync';
import type {
    SyncBillRequestDto,
    SyncMemberCollectionDto,
    SyncExpenseCollectionDto,
    SyncMemberDto,
    SyncExpenseDto,
    SyncExpenseItemDto,
} from '@/api/models';
import { useToast } from '@/shared/hooks/use-toast';

/**
 * 將本地 Bill 轉換為 SyncBillRequestDto（首次同步用）
 */
function billToSyncRequest(bill: Bill): SyncBillRequestDto {
    const members: SyncMemberCollectionDto = {
        upsert: bill.members.map((m, index): SyncMemberDto => ({
            localId: m.id,
            name: m.name,
            displayOrder: index,
            linkedUserId: m.userId,
            claimedAt: m.claimedAt,
        })),
        deletedIds: [],
    };

    const expenses: SyncExpenseCollectionDto = {
        upsert: bill.expenses.map((e): SyncExpenseDto => ({
            localId: e.id,
            name: e.name,
            amount: e.amount,
            serviceFeePercent: e.serviceFeePercent,
            isItemized: e.isItemized,
            paidByLocalId: e.paidById,
            participantLocalIds: e.participants,
            items: e.isItemized ? {
                upsert: e.items.map((i): SyncExpenseItemDto => ({
                    localId: i.id,
                    name: i.name,
                    amount: i.amount,
                    paidByLocalId: i.paidById,
                    participantLocalIds: i.participants,
                })),
                deletedIds: [],
            } : undefined,
        })),
        deletedIds: [],
    };

    return {
        localId: bill.id,
        remoteId: bill.remoteId || null, // 確保空字串轉為 null
        baseVersion: bill.version,
        name: bill.name,
        members,
        expenses,
        settledTransfers: bill.settledTransfers,
        localUpdatedAt: bill.updatedAt,
    };
}

/**
 * 解析 API 錯誤為 SyncError
 */
function parseApiError(error: unknown): SyncError {
    // 處理 MissingRemoteIdError（資料完整性錯誤，不可重試）
    if (isMissingRemoteIdError(error)) {
        return {
            code: 'VALIDATION_ERROR',
            message: '同步資料不完整，請重新整理頁面後再試',
            retryable: false,
            details: {
                entityType: error.entityType,
                localId: error.localId,
            },
        };
    }

    if (error instanceof Error) {
        const message = error.message.toLowerCase();

        if (message.includes('network') || message.includes('fetch')) {
            return {
                code: 'NETWORK_ERROR',
                message: '網路連線失敗，請檢查網路狀態',
                retryable: true,
            };
        }

        if (message.includes('401') || message.includes('unauthorized')) {
            return {
                code: 'UNAUTHORIZED',
                message: '請先登入後再同步',
                retryable: false,
            };
        }

        if (message.includes('409') || message.includes('conflict')) {
            return {
                code: 'VERSION_CONFLICT',
                message: '版本衝突，正在取得最新版本',
                retryable: true,
            };
        }

        if (message.includes('400') || message.includes('validation')) {
            return {
                code: 'VALIDATION_ERROR',
                message: '資料驗證失敗',
                retryable: false,
            };
        }

        if (message.includes('500') || message.includes('server')) {
            return {
                code: 'SERVER_ERROR',
                message: '伺服器錯誤，請稍後再試',
                retryable: true,
            };
        }
    }

    return {
        code: 'UNKNOWN',
        message: '同步失敗，請稍後再試',
        retryable: true,
    };
}

/**
 * 全域同步鎖 - 防止同一帳單並發同步
 * 使用 Map<billId, Promise<SyncResult>> 追蹤進行中的同步操作
 */
const syncLocks = new Map<string, Promise<SyncResult>>();

export function useBillSync() {
    const [isUploading, setIsUploading] = useState(false);
    const [isDownloading, setIsDownloading] = useState(false);
    const [uploadError, setUploadError] = useState<SyncError | null>(null);
    const [downloadError, setDownloadError] = useState<SyncError | null>(null);
    const { toast } = useToast();

    // 用於防止重複 Toast 通知
    const lastToastRef = useRef<{ type: 'success' | 'error'; time: number }>({ type: 'success', time: 0 });

    const {
        setBillSyncStatus,
        setBillRemoteId,
        applyIdMappings,
        markBillAsSynced,
        rebaseBillFromServer,
        importBill,
    } = useSnapSplitStore();

    // Get SyncQueue's updateQueueWithIdMappings function
    const updateQueueWithIdMappings = useSyncQueue(state => state.updateQueueWithIdMappings);

    const syncBillMutation = useSyncBill();
    const deltaSyncMutation = useDeltaSyncBill();

    // 只顯示錯誤 Toast（成功由 UI 狀態反映）
    const showToast = useCallback((type: 'success' | 'error', title: string, description: string) => {
        // 成功不顯示 Toast，由 SyncStatusIndicator 反映狀態
        if (type === 'success') return;

        const now = Date.now();
        const lastToast = lastToastRef.current;

        // 5 秒內不重複顯示同類型通知
        if (lastToast.type === type && now - lastToast.time < 5000) {
            return;
        }

        lastToastRef.current = { type, time: now };

        toast({
            variant: 'destructive',
            title,
            description,
        });
    }, [toast]);

    /**
     * 內部同步實作（不含鎖）
     */
    const performSyncInternal = useCallback(async (bill: Bill): Promise<SyncResult> => {
        setIsUploading(true);
        setUploadError(null);
        setBillSyncStatus(bill.id, 'syncing');

        try {
            let result: SyncResult;

            if (!bill.remoteId) {
                // 首次同步：使用 syncBill API
                result = await performFullSync(bill);
            } else {
                // 增量同步：使用 deltaSyncBill API
                result = await performDeltaSync(bill);
            }

            if (result.success) {
                // 套用 ID 映射
                if (result.idMappings) {
                    applyIdMappings(bill.id, result.idMappings);
                    // Update SyncQueue pending actions with new ID mappings
                    updateQueueWithIdMappings(result.idMappings);
                }

                // 檢查是否有合併結果（版本衝突時 ADD 操作已合併，需用 latestBill 重建狀態）
                if (result.mergedBill) {
                    rebaseBillFromServer(bill.id, result.mergedBill as any);
                    showToast('success', '已合併', '帳單已與雲端版本合併');
                } else {
                    // 更新版本號（Delta Sync 返回的新版本）
                    if (result.newVersion && result.newVersion > bill.version) {
                        setBillRemoteId(bill.id, bill.remoteId ?? '', bill.shareCode, result.newVersion);
                    }
                    // 標記為已同步
                    markBillAsSynced(bill.id, bill.updatedAt);
                    showToast('success', '已同步', `「${bill.name}」已同步到雲端`);
                }
            } else if (result.mergedBill) {
                // 同步失敗但有合併結果（Delta Sync 衝突情況）
                rebaseBillFromServer(bill.id, result.mergedBill as any);
                showToast('success', '已合併', '帳單已與雲端版本合併');
            } else if (result.error) {
                setBillSyncStatus(bill.id, 'error', result.error.message);
                setUploadError(result.error);
                showToast('error', '同步失敗', result.error.message);
            }

            return result;
        } catch (error) {
            const syncError = parseApiError(error);
            setBillSyncStatus(bill.id, 'error', syncError.message);
            setUploadError(syncError);
            showToast('error', '同步失敗', syncError.message);
            return { success: false, error: syncError };
        } finally {
            setIsUploading(false);
        }
    }, [setBillSyncStatus, applyIdMappings, markBillAsSynced, rebaseBillFromServer, showToast, updateQueueWithIdMappings]);

    /**
     * 同步帳單到雲端（帶並發控制）
     *
     * 如果同一帳單已有同步進行中，會等待該同步完成並返回其結果，
     * 避免重複發送請求導致帳單重複或版本衝突。
     */
    const syncBill = useCallback(async (bill: Bill): Promise<SyncResult> => {
        if (!bill) {
            return { success: false, error: { code: 'UNKNOWN', message: '無效的帳單', retryable: false } };
        }

        // 檢查是否已有同步進行中
        const existingSync = syncLocks.get(bill.id);
        if (existingSync) {
            return existingSync;
        }

        // 建立新的同步 Promise 並註冊到鎖
        const syncPromise = performSyncInternal(bill);
        syncLocks.set(bill.id, syncPromise);

        try {
            return await syncPromise;
        } finally {
            // 同步完成後釋放鎖
            syncLocks.delete(bill.id);
        }
    }, [performSyncInternal]);

    /**
     * 執行首次完整同步
     */
    const performFullSync = async (bill: Bill): Promise<SyncResult> => {
        const request = billToSyncRequest(bill);

        let response;
        try {
            response = await syncBillMutation.mutateAsync({ data: request });
        } catch (error) {
            console.error('[Sync] Full sync API error:', error);
            throw error;
        }

        if (!response.success || !response.data) {
            return {
                success: false,
                error: {
                    code: 'SERVER_ERROR',
                    message: response.error?.message ?? '同步失敗',
                    retryable: true,
                },
            };
        }

        const data = response.data;

        // 設定遠端 ID 和分享碼
        setBillRemoteId(bill.id, data.remoteId!, data.shareCode ?? undefined, data.version ?? 1);

        // 解析 ID 映射
        const idMappings = parseIdMappings(data.idMappings as any);

        // 檢查是否有版本衝突（後端返回 latestBill 表示有衝突但 ADD 已合併）
        if (data.latestBill) {
            const mergedBill = billDtoToBill(data.latestBill);
            return {
                success: true, // 設為 true 因為 ADD 操作已合併成功
                newVersion: data.version ?? 1,
                idMappings,
                mergedBill, // 前端需用此重建狀態
            };
        }

        return {
            success: true,
            newVersion: data.version ?? 1,
            idMappings,
        };
    };

    /**
     * 執行增量同步
     */
    const performDeltaSync = async (bill: Bill): Promise<SyncResult> => {
        // 使用 syncSnapshot 計算 delta（若無 snapshot 則視為首次同步後的第一次變更）
        const deltaRequest = buildDeltaSyncRequest(bill, bill.syncSnapshot);

        // 如果沒有變更，直接返回成功
        if (isDeltaEmpty(deltaRequest)) {
            markBillAsSynced(bill.id, bill.updatedAt);
            return { success: true, newVersion: bill.version };
        }

        let response;
        try {
            response = await deltaSyncMutation.mutateAsync({
                id: bill.remoteId!,
                data: deltaRequest,
            });
        } catch (error) {
            console.error('[Sync] Delta sync API error:', error);
            throw error;
        }

        if (!response.success || !response.data) {
            return {
                success: false,
                error: {
                    code: 'SERVER_ERROR',
                    message: response.error?.message ?? '同步失敗',
                    retryable: true,
                },
            };
        }

        const data = response.data;

        // 處理衝突
        if (data.conflicts && data.conflicts.length > 0) {
            // 如果有 mergedBill，使用它重建狀態
            if (data.mergedBill) {
                return {
                    success: false,
                    newVersion: data.newVersion,
                    mergedBill: data.mergedBill as any,
                    conflicts: data.conflicts.map(c => ({
                        entityType: c.type as any ?? 'bill',
                        entityId: c.entityId ?? '',
                        field: c.field ?? '',
                        localValue: c.localValue,
                        serverValue: c.serverValue,
                        resolvedValue: c.resolvedValue,
                    })),
                };
            }
        }

        // 解析 ID 映射
        const idMappings = parseIdMappings(data.idMappings);

        return {
            success: data.success ?? true,
            newVersion: data.newVersion,
            idMappings,
        };
    };

    /**
     * 根據分享碼取得帳單並匯入到本地 store
     */
    const fetchBillByShareCode = useCallback(async (shareCode: string): Promise<Bill> => {
        if (!shareCode) {
            throw new Error('分享碼不可為空');
        }

        setIsDownloading(true);
        setDownloadError(null);

        try {
            const response = await getBillByShareCode(shareCode);

            if (!response.success || !response.data) {
                throw new Error(response.error?.message ?? '找不到帳單');
            }

            const bill = billDtoToBill(response.data);

            // 將帳單匯入到本地 store
            importBill(bill);

            return bill;
        } catch (error) {
            const syncError = parseApiError(error);
            setDownloadError(syncError);
            throw error;
        } finally {
            setIsDownloading(false);
        }
    }, [importBill]);

    return {
        syncBill,
        fetchBillByShareCode,
        isUploading,
        isDownloading,
        uploadError,
        downloadError,
    };
}

/**
 * 批次同步所有未同步的帳單
 */
export function useBatchSync() {
    const { getUnsyncedBills } = useSnapSplitStore();
    const { syncBill } = useBillSync();
    const [isSyncing, setIsSyncing] = useState(false);

    const syncAllUnsynced = useCallback(async (): Promise<SyncResult[]> => {
        const unsyncedBills = getUnsyncedBills();
        if (unsyncedBills.length === 0) {
            return [];
        }

        setIsSyncing(true);
        const results: SyncResult[] = [];

        try {
            for (const bill of unsyncedBills) {
                const result = await syncBill(bill);
                results.push(result);

                // 如果遇到認證錯誤，停止同步
                if (result.error?.code === 'UNAUTHORIZED') {
                    break;
                }
            }
        } finally {
            setIsSyncing(false);
        }

        return results;
    }, [getUnsyncedBills, syncBill]);

    return {
        syncAllUnsynced,
        isSyncing,
    };
}

// Legacy export for compatibility
export function useSyncBillMutation() {
    return useBillSync();
}
