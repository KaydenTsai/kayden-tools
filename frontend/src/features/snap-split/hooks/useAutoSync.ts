/**
 * useAutoSync - 自動同步 Hook
 *
 * 功能：
 * 1. 偵測本地變更並自動觸發同步（防抖 2 秒）
 * 2. 網路恢復時自動同步未同步的帳單
 * 3. 失敗重試機制（最多 3 次，指數退避）
 */

import { useEffect, useRef, useCallback, useState } from 'react';
import { useSnapSplitStore, useCurrentBill } from '../stores/snapSplitStore';
import { useBillSync } from './useBillSync';
import { useNetworkStatus } from './useNetworkStatus';
import { useAuthStore } from '@/stores/authStore';
import { deleteBill as deleteBillApi } from '@/api/endpoints/bills/bills';
import type { Bill } from '../types/snap-split';

/** 自動同步配置 */
interface AutoSyncConfig {
    /** 防抖延遲（毫秒） */
    debounceMs?: number;
    /** 最大重試次數 */
    maxRetries?: number;
    /** 是否啟用自動同步 */
    enabled?: boolean;
}

/**
 * 自動同步開關
 * - Step 1: 後端冪等性已修復（LocalClientId + 唯一索引）
 * - Step 2: 前端並發控制已加入（syncLocks）
 */
const TEMP_DISABLE_AUTO_SYNC = false;

const DEFAULT_CONFIG: Required<AutoSyncConfig> = {
    debounceMs: 500,
    maxRetries: 3,
    enabled: true,
};

interface AutoSyncState {
    /** 待同步的帳單數量 */
    pendingSyncCount: number;
    /** 是否正在自動同步 */
    isAutoSyncing: boolean;
    /** 最後同步時間 */
    lastSyncAt: Date | null;
    /** 重試次數 */
    retryCount: number;
}

export function useAutoSync(config: AutoSyncConfig = {}) {
    const { debounceMs, maxRetries, enabled } = { ...DEFAULT_CONFIG, ...config };

    const currentBill = useCurrentBill();
    const { getUnsyncedBills } = useSnapSplitStore();
    const { syncBill, isUploading } = useBillSync();
    const { isAuthenticated } = useAuthStore();

    const [state, setState] = useState<AutoSyncState>({
        pendingSyncCount: 0,
        isAutoSyncing: false,
        lastSyncAt: null,
        retryCount: 0,
    });

    // 追蹤上一次的 bill 狀態用於變更偵測
    const prevBillRef = useRef<string | null>(null);
    const debounceTimerRef = useRef<NodeJS.Timeout | null>(null);
    const retryTimerRef = useRef<NodeJS.Timeout | null>(null);

    // 序列化同步：鎖 + 待處理變更旗標
    const isSyncingRef = useRef(false);
    const hasPendingChangesRef = useRef(false);
    const pendingBillRef = useRef<Bill | null>(null);

    /**
     * 內部同步實作（帶重試邏輯）
     */
    const performSyncInternal = useCallback(async (bill: Bill, retryCount = 0): Promise<void> => {
        try {
            const result = await syncBill(bill);

            if (result.success) {
                setState(prev => ({
                    ...prev,
                    lastSyncAt: new Date(),
                    retryCount: 0,
                }));
            } else if (result.error?.retryable && retryCount < maxRetries) {
                const delay = Math.pow(2, retryCount) * 1000;
                setState(prev => ({ ...prev, retryCount: retryCount + 1 }));
                await new Promise(resolve => {
                    retryTimerRef.current = setTimeout(resolve, delay);
                });
                await performSyncInternal(bill, retryCount + 1);
            }
        } catch {
            if (retryCount < maxRetries) {
                const delay = Math.pow(2, retryCount) * 1000;
                setState(prev => ({ ...prev, retryCount: retryCount + 1 }));
                await new Promise(resolve => {
                    retryTimerRef.current = setTimeout(resolve, delay);
                });
                await performSyncInternal(bill, retryCount + 1);
            }
        }
    }, [syncBill, maxRetries]);

    /**
     * 序列化同步：取得鎖、執行同步、檢查待處理變更
     */
    const performSync = useCallback(async (bill: Bill): Promise<void> => {
        if (!enabled || !isAuthenticated) return;

        // 若已在同步中，標記有待處理變更並儲存最新 bill
        if (isSyncingRef.current) {
            console.log('[AutoSync] Sync in progress, marking pending changes');
            hasPendingChangesRef.current = true;
            pendingBillRef.current = bill;
            return;
        }

        // 取得鎖
        isSyncingRef.current = true;
        hasPendingChangesRef.current = false;
        setState(prev => ({ ...prev, isAutoSyncing: true }));

        try {
            await performSyncInternal(bill);

            // 同步完成後檢查是否有待處理變更
            while (hasPendingChangesRef.current && pendingBillRef.current) {
                console.log('[AutoSync] Processing pending changes');
                const nextBill = pendingBillRef.current;
                hasPendingChangesRef.current = false;
                pendingBillRef.current = null;
                await performSyncInternal(nextBill);
            }
        } finally {
            // 釋放鎖
            isSyncingRef.current = false;
            setState(prev => ({ ...prev, isAutoSyncing: false }));
        }
    }, [enabled, isAuthenticated, performSyncInternal]);

    /**
     * 觸發防抖同步
     */
    const triggerDebouncedSync = useCallback((bill: Bill) => {
        if (debounceTimerRef.current) {
            clearTimeout(debounceTimerRef.current);
        }

        debounceTimerRef.current = setTimeout(() => {
            performSync(bill);
        }, debounceMs);
    }, [debounceMs, performSync]);

    /**
     * 監聽當前帳單變更
     */
    useEffect(() => {
        // ⚠️ 臨時停用自動同步
        if (TEMP_DISABLE_AUTO_SYNC) {
            return;
        }

        if (!enabled || !currentBill || !isAuthenticated) return;

        // 使用 JSON 序列化來檢測實際變更
        const billSignature = JSON.stringify({
            name: currentBill.name,
            members: currentBill.members,
            expenses: currentBill.expenses,
            settledTransfers: currentBill.settledTransfers,
        });

        console.log('[AutoSync] Check:', {
            hasRemoteId: !!currentBill.remoteId,
            syncStatus: currentBill.syncStatus,
            signatureChanged: prevBillRef.current !== billSignature,
        });

        // 情況 1: 已同步過的帳單（有 remoteId）且狀態變為 modified
        if (currentBill.remoteId && currentBill.syncStatus === 'modified') {
            if (prevBillRef.current !== billSignature) {
                console.log('[AutoSync] Triggering sync for modified bill');
                prevBillRef.current = billSignature;
                triggerDebouncedSync(currentBill);
            }
        }

        // 情況 2: 新帳單（無 remoteId）- 首次同步
        // 條件：狀態為 local、有實際內容
        // 注意：error 狀態不自動重試，需使用者手動觸發
        if (!currentBill.remoteId &&
            currentBill.syncStatus === 'local' &&
            (currentBill.members.length > 0 || currentBill.expenses.length > 0)) {
            if (prevBillRef.current !== billSignature) {
                prevBillRef.current = billSignature;
                triggerDebouncedSync(currentBill);
            }
        }
    }, [enabled, currentBill, isAuthenticated, triggerDebouncedSync]);

    /**
     * 網路恢復時同步所有未同步帳單
     */
    const handleNetworkRestore = useCallback(async () => {
        // ⚠️ 臨時停用
        if (TEMP_DISABLE_AUTO_SYNC) return;

        if (!enabled || !isAuthenticated) return;

        const unsyncedBills = getUnsyncedBills();
        if (unsyncedBills.length === 0) return;

        setState(prev => ({ ...prev, isAutoSyncing: true }));

        for (const bill of unsyncedBills) {
            // 軟刪除的帳單 → 呼叫 DELETE API → 成功後硬刪除
            if (bill.isDeleted && bill.remoteId && bill.syncStatus === 'modified') {
                try {
                    await deleteBillApi(bill.remoteId);
                    useSnapSplitStore.getState().deleteBill(bill.id);
                } catch {
                    // 下次恢復再重試
                }
                continue;
            }

            // 同步已有 remoteId 且狀態為 modified 的帳單
            // 或有內容的新帳單（狀態為 local）
            // 注意：error 狀態的帳單不自動重試
            const shouldSync = (bill.remoteId && bill.syncStatus === 'modified') ||
                (!bill.remoteId && bill.syncStatus === 'local' &&
                    (bill.members.length > 0 || bill.expenses.length > 0));

            if (shouldSync) {
                await performSync(bill);
            }
        }

        setState(prev => ({ ...prev, isAutoSyncing: false }));
    }, [enabled, isAuthenticated, getUnsyncedBills, performSync]);

    // 監聽網路狀態
    useNetworkStatus({
        onOnline: handleNetworkRestore,
    });

    /**
     * 啟動時處理殘留的軟刪除帳單
     */
    useEffect(() => {
        if (!enabled || !isAuthenticated) return;

        const processDeletedBills = async () => {
            const bills = useSnapSplitStore.getState().bills;
            const deletedBills = bills.filter(b => b.isDeleted && b.remoteId && b.syncStatus === 'modified');

            for (const bill of deletedBills) {
                try {
                    await deleteBillApi(bill.remoteId!);
                    useSnapSplitStore.getState().deleteBill(bill.id);
                } catch {
                    // 靜默失敗，等網路恢復再重試
                }
            }
        };

        processDeletedBills();
    }, [enabled, isAuthenticated]);

    /**
     * 更新待同步數量
     */
    useEffect(() => {
        const unsyncedBills = getUnsyncedBills();
        // 包含已有 remoteId 的帳單，以及有內容的新帳單
        const syncableBills = unsyncedBills.filter(b =>
            b.remoteId || (b.members.length > 0 || b.expenses.length > 0)
        );
        setState(prev => ({
            ...prev,
            pendingSyncCount: syncableBills.length,
        }));
    }, [getUnsyncedBills, currentBill?.syncStatus]);

    /**
     * 清理計時器
     */
    useEffect(() => {
        return () => {
            if (debounceTimerRef.current) {
                clearTimeout(debounceTimerRef.current);
            }
            if (retryTimerRef.current) {
                clearTimeout(retryTimerRef.current);
            }
        };
    }, []);

    /**
     * 手動觸發同步
     */
    const syncNow = useCallback(async () => {
        if (!currentBill) return;
        await performSync(currentBill);
    }, [currentBill, performSync]);

    return {
        ...state,
        isSyncing: state.isAutoSyncing || isUploading,
        syncNow,
    };
}
