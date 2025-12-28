import { useEffect, useRef } from 'react';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { useBillSync } from './useBillSync';

const AUTO_SYNC_DEBOUNCE_MS = 2000;

/**
 * 自動同步 Hook
 * 登入用戶的帳單自動同步到雲端：
 * - 新建立的帳單（local, 無 remoteId）
 * - 已同步但被修改的帳單（modified, 有 remoteId）
 */
export function useAutoSync(isAuthenticated: boolean) {
    const bills = useSnapSplitStore(state => state.bills);
    const { syncBill, isUploading } = useBillSync();
    const syncTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const syncingBillIds = useRef<Set<string>>(new Set());
    const isUploadingRef = useRef(isUploading);
    const syncBillRef = useRef(syncBill);

    // 更新 ref 以避免 stale closure
    isUploadingRef.current = isUploading;
    syncBillRef.current = syncBill;

    // 找出需要自動同步的帳單 ID（用 ID 比較避免物件引用問題）
    const billIdsToSync = bills
        .filter(bill => {
            if (bill.isSnapshot) return false;
            if (bill.remoteId && bill.syncStatus === 'modified') return true;
            if (!bill.remoteId && bill.syncStatus === 'local') return true;
            return false;
        })
        .map(b => b.id);

    useEffect(() => {
        if (!isAuthenticated || billIdsToSync.length === 0) {
            return;
        }

        if (import.meta.env.DEV) {
            console.log('[AutoSync] Bills to sync:', billIdsToSync);
        }

        // 清除之前的 timeout
        if (syncTimeoutRef.current) {
            clearTimeout(syncTimeoutRef.current);
        }

        // Debounce：等待用戶停止編輯後再同步
        syncTimeoutRef.current = setTimeout(async () => {
            if (isUploadingRef.current) {
                if (import.meta.env.DEV) {
                    console.log('[AutoSync] Skipped - already uploading');
                }
                return;
            }

            // 從 store 取得最新的帳單資料
            const currentBills = useSnapSplitStore.getState().bills;

            for (const billId of billIdsToSync) {
                if (syncingBillIds.current.has(billId)) continue;

                const bill = currentBills.find(b => b.id === billId);
                if (!bill) continue;

                // 再次確認需要同步
                const needsSync =
                    (!bill.remoteId && bill.syncStatus === 'local') ||
                    (bill.remoteId && bill.syncStatus === 'modified');

                if (!needsSync) continue;

                syncingBillIds.current.add(billId);
                try {
                    if (import.meta.env.DEV) {
                        console.log('[AutoSync] Syncing bill:', billId, bill.name);
                    }
                    await syncBillRef.current(bill);
                    if (import.meta.env.DEV) {
                        console.log('[AutoSync] Sync success:', billId);
                    }
                } catch (err) {
                    console.warn('[AutoSync] Failed to sync bill:', billId, err);
                } finally {
                    syncingBillIds.current.delete(billId);
                }
            }
        }, AUTO_SYNC_DEBOUNCE_MS);

        return () => {
            if (syncTimeoutRef.current) {
                clearTimeout(syncTimeoutRef.current);
            }
        };
    // 使用 ref 避免 syncBill 造成不必要的 re-trigger
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [isAuthenticated, billIdsToSync.join(',')]);

    return {
        pendingSyncCount: billIdsToSync.length,
    };
}
