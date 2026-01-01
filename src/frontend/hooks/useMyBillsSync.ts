import { useState, useCallback, useEffect } from 'react';
import { getMyBills } from '@/api/endpoints/bills/bills';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { billDtoToBill } from '@/adapters/billAdapter';
import type { BillDto } from '@/api/models';

interface UseMyBillsSyncResult {
    isFetching: boolean;
    error: string | null;
    fetchMyBills: () => Promise<BillDto[]>;
    syncMyBills: () => Promise<{ fetched: number; merged: number }>;
}

// const POLL_INTERVAL = 10000; // 10 秒 - 暫時禁用

/**
 * 同步使用者參與的帳單 Hook
 * 用於登入後從雲端拉取帳單並合併到本地
 * @param autoPoll 是否啟用自動輪詢 (預設: false)
 */
export function useMyBillsSync(autoPoll: boolean = false): UseMyBillsSyncResult {
    const { importBillsFromRemote } = useSnapSplitStore();
    const [isFetching, setIsFetching] = useState(false);
    const [error, setError] = useState<string | null>(null);

    /**
     * 從雲端取得使用者參與的帳單
     */
    const fetchMyBills = useCallback(async (): Promise<BillDto[]> => {
        setIsFetching(true);
        setError(null);

        try {
            const response = await getMyBills();
            if (response.success && response.data) {
                return response.data;
            }
            throw new Error(response.error?.message ?? 'Failed to fetch bills');
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Unknown error';
            setError(message);
            // 靜默失敗，不影響使用者操作
            if (import.meta.env.DEV) {
                console.warn('[MyBillsSync] Failed to fetch my bills:', err);
            }
            return [];
        } finally {
            setIsFetching(false);
        }
    }, []);

    /**
     * 完整同步流程：取得 + 合併
     * 使用 store 的 importBillsFromRemote 避免 stale closure 問題
     */
    const syncMyBills = useCallback(async (): Promise<{ fetched: number; merged: number }> => {
        // 如果正在 fetch，避免重複呼叫
        // 但這裡我們不檢查 isFetching 狀態，因為 fetchMyBills 內部會設為 true，
        // 且我們希望 polling 時不要因為 UI 顯示 loading 而卡住，這裡 isFetching 主要用於 UI loading spinner
        
        const remoteBills = await fetchMyBills();

        // 轉換為本地格式
        // 注意：這裡應該也要處理「被刪除的帳單」。
        // 目前 importBillsFromRemote 只負責「新增或更新」，不負責「刪除本地有但遠端沒有的帳單」。
        // 這是一個需要處理的點。
        
        const billsToImport = remoteBills
            .filter(dto => dto.id) // 確保有 ID
            .map(dto => billDtoToBill(dto, 'synced'));

        // 使用 store 方法匯入（會自動檢查重複，並刪除不在列表中的同步帳單）
        const merged = importBillsFromRemote(billsToImport, 'replace_list');

        return { fetched: remoteBills.length, merged };
    }, [fetchMyBills, importBillsFromRemote]);

    // Polling Effect
    // TODO: 暫時禁用輪詢以調試 SignalR 操作
    useEffect(() => {
        if (!autoPoll) return;

        if (import.meta.env.DEV) {
            console.log('[MyBillsSync] Polling DISABLED for debugging');
        }

        // 暫時禁用：不進行輪詢
        // // 立即執行一次
        // syncMyBills();
        //
        // const intervalId = setInterval(() => {
        //     syncMyBills();
        // }, POLL_INTERVAL);
        //
        // return () => clearInterval(intervalId);
    }, [autoPoll, syncMyBills]);

    return {
        isFetching,
        error,
        fetchMyBills,
        syncMyBills,
    };
}
