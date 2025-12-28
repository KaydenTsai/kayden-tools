import { useState, useCallback } from 'react';
import { getApiBillsMine } from '@/api/endpoints/bills/bills';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { billDtoToBill } from '@/adapters/billAdapter';
import type { BillDto } from '@/api/models';

interface UseMyBillsSyncResult {
    isFetching: boolean;
    error: string | null;
    fetchMyBills: () => Promise<BillDto[]>;
    syncMyBills: () => Promise<{ fetched: number; merged: number }>;
}

/**
 * 同步使用者參與的帳單 Hook
 * 用於登入後從雲端拉取帳單並合併到本地
 */
export function useMyBillsSync(): UseMyBillsSyncResult {
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
            const response = await getApiBillsMine();
            if (import.meta.env.DEV) {
                console.log('[MyBillsSync] API response:', response);
            }
            if (response.success && response.data) {
                return response.data;
            }
            throw new Error(response.error?.message ?? 'Failed to fetch bills');
        } catch (err) {
            const message = err instanceof Error ? err.message : 'Unknown error';
            setError(message);
            console.error('[MyBillsSync] Failed to fetch my bills:', err);
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
        const remoteBills = await fetchMyBills();

        if (remoteBills.length === 0) {
            return { fetched: 0, merged: 0 };
        }

        // 轉換為本地格式
        const billsToImport = remoteBills
            .filter(dto => dto.id) // 確保有 ID
            .map(dto => billDtoToBill(dto, 'synced'));

        if (import.meta.env.DEV) {
            console.log('[MyBillsSync] Bills to import:', billsToImport.length);
        }

        // 使用 store 方法匯入（會自動檢查重複）
        const merged = importBillsFromRemote(billsToImport);

        if (import.meta.env.DEV) {
            console.log('[MyBillsSync] Merged:', merged);
        }

        return { fetched: remoteBills.length, merged };
    }, [fetchMyBills, importBillsFromRemote]);

    return {
        isFetching,
        error,
        fetchMyBills,
        syncMyBills,
    };
}
