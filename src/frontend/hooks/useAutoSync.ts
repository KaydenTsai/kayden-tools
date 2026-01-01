import { useEffect, useRef } from 'react';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { useBillSync } from './useBillSync';

const AUTO_SYNC_INTERVAL_MS = 3000; // 每 3 秒檢查一次

/**
 * 自動同步 Hook（使用 Interval 避免 effect 無限觸發）
 */
export function useAutoSync(isAuthenticated: boolean) {
    const { syncBill } = useBillSync();
    const syncingBillIds = useRef<Set<string>>(new Set());
    const syncBillRef = useRef(syncBill);

    // 更新 ref 以避免 stale closure
    syncBillRef.current = syncBill;

    useEffect(() => {
        if (!isAuthenticated) return;

        const checkAndSync = async () => {
            // 如果已經有正在同步的帳單，跳過這次檢查
            if (syncingBillIds.current.size > 0) {
                if (import.meta.env.DEV) {
                    console.log('[AutoSync] Skipped - already syncing:', Array.from(syncingBillIds.current));
                }
                return;
            }

            // 從 store 取得最新的帳單資料
            const currentBills = useSnapSplitStore.getState().bills;

            // 找出需要同步的帳單
            // 注意：有 remoteId 的帳單應該透過 SignalR 同步操作，這裡只處理：
            // 1. 首次同步（沒有 remoteId 的本地帳單）
            // 2. SignalR 無法使用時的備援同步（暫時禁用）
            // const connectionStatus = useSnapSplitStore.getState().connectionStatus;
            const billsToSync = currentBills.filter(bill => {
                if (bill.isSnapshot) return false;
                if (syncingBillIds.current.has(bill.id)) return false;

                // 沒有 remoteId 的帳單（首次同步）
                if (!bill.remoteId && (bill.syncStatus === 'local' || bill.syncStatus === 'modified' || bill.syncStatus === 'error')) {
                    if (import.meta.env.DEV) {
                        console.log('[AutoSync] Will sync (no remoteId):', bill.name);
                    }
                    return true;
                }

                // TODO: 暫時禁用備援同步以調試 SignalR 操作
                // 有 remoteId 但 SignalR 未連線時，作為備援同步
                // 注意：這會使用 REST API 完整同步，可能導致版本衝突
                // if (bill.remoteId && connectionStatus !== 'connected') {
                //     if (bill.syncStatus === 'modified' || bill.syncStatus === 'error') {
                //         return true;
                //     }
                // }
                return false;
            });

            if (billsToSync.length === 0) return;

            if (import.meta.env.DEV) {
                console.log('[AutoSync] Bills to sync:', billsToSync.map(b => b.name));
            }

            // 同步每個帳單
            for (const bill of billsToSync) {
                syncingBillIds.current.add(bill.id);
                try {
                    if (import.meta.env.DEV) {
                        console.log('[AutoSync] Syncing bill:', bill.id, bill.name);
                    }
                    await syncBillRef.current(bill);
                    if (import.meta.env.DEV) {
                        console.log('[AutoSync] Sync success:', bill.id);
                    }
                } catch (err) {
                    console.warn('[AutoSync] Failed to sync bill:', bill.id, err);
                } finally {
                    syncingBillIds.current.delete(bill.id);
                }
            }
        };

        // 立即檢查一次
        checkAndSync();

        // 設定定期檢查
        const intervalId = setInterval(checkAndSync, AUTO_SYNC_INTERVAL_MS);

        return () => clearInterval(intervalId);
    }, [isAuthenticated]);

    return {
        pendingSyncCount: 0, // 不再追蹤，避免觸發 re-render
    };
}
