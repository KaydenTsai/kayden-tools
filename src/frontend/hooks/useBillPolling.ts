import { useEffect, useRef } from 'react';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { getBillByShareCode } from '@/api';
import type { BillDtoApiResponse } from '@/api/models';
import { billDtoToBill } from '@/adapters/billAdapter';

const POLL_INTERVAL = 5000; // 5 秒輪詢一次詳細資料

/**
 * 單張帳單輪詢 Hook
 * 當使用者正在查看某張已同步的帳單時，定期檢查雲端版本
 * @param billId 當前查看的帳單 ID
 * @param isActive 是否啟用輪詢
 *
 * @deprecated 暫時禁用輪詢以調試 SignalR 操作
 */
export function useBillPolling(billId: string | null, isActive: boolean) {
    // TODO: 暫時完全禁用輪詢，只使用 SignalR 操作
    if (import.meta.env.DEV) {
        console.log('[BillPolling] DISABLED for debugging');
    }
    return;

    // 使用 ref 避免 effect 依賴 store 函數
    const processingRef = useRef(false);

    useEffect(() => {
        if (!billId || !isActive) return;

        const poll = async () => {
            if (processingRef.current) return;

            // 取得當前帳單狀態
            const currentBill = useSnapSplitStore.getState().bills.find(b => b.id === billId);

            // 只對已同步且有 shareCode (或 remoteId) 的帳單進行輪詢
            // 如果有本地修改 (modified)，跳過輪詢以避免覆蓋本地資料
            if (!currentBill || !currentBill.remoteId || !currentBill.shareCode) {
                return;
            }

            // 如果帳單有本地修改，不要從後端拉取（避免覆蓋）
            if (currentBill.syncStatus === 'modified' || currentBill.syncStatus === 'syncing') {
                if (import.meta.env.DEV) {
                    console.log('[BillPolling] Skipped - bill has local modifications:', currentBill.syncStatus);
                }
                return;
            }

            processingRef.current = true;
            try {
                // 使用 shareCode 查詢最新狀態 (因為這是公開讀取的 API，負擔較小)
                // 注意：這裡假設 shareCode 可以查到完整資訊。如果需要權限控制，應該改用 getById API
                const response = await getBillByShareCode(currentBill.shareCode);
                const apiResponse = response as BillDtoApiResponse;

                if (apiResponse.success && apiResponse.data) {
                    const remoteBill = apiResponse.data;
                    
                    // 檢查版本號
                    // 如果遠端版本 > 本地版本，則進行合併
                    if (remoteBill.version && remoteBill.version > currentBill.version) {
                        if (import.meta.env.DEV) {
                            console.log(`[BillPolling] New version detected for ${currentBill.name}: v${remoteBill.version} > v${currentBill.version}`);
                        }
                        
                        // 轉換並匯入
                        const newBill = billDtoToBill(remoteBill, 'synced');
                        
                        // 強制更新本地，因為伺服器版本較新
                        useSnapSplitStore.getState().importBillsFromRemote([newBill]);
                    }
                }
            } catch (error) {
                // 輪詢失敗不需報錯，默默略過
                if (import.meta.env.DEV) {
                    console.warn('[BillPolling] Poll failed:', error);
                }
            } finally {
                processingRef.current = false;
            }
        };

        // 立即執行一次
        poll();

        const intervalId = setInterval(poll, POLL_INTERVAL);
        return () => clearInterval(intervalId);
    }, [billId, isActive]);

}
