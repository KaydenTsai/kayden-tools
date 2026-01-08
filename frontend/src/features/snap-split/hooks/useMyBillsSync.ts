/**
 * useMyBillsSync - 我的帳單同步 Hook
 *
 * 功能：
 * 1. 從雲端拉取用戶的帳單列表
 * 2. 與本地帳單合併（不重複）
 * 3. 登入後自動同步
 */

import { useState, useCallback } from 'react';
import { useSnapSplitStore } from '../stores/snapSplitStore';
import { useGetMyBills } from '@/api/endpoints/bills/bills';
import { billDtoToBill } from '../services/billAdapter';
import { useAuthStore } from '@/stores/authStore';
import type { Bill } from '../types/snap-split';
import type { SyncError } from '../types/sync';
import type { BillDto } from '@/api/models';

interface MyBillsSyncResult {
    /** 從雲端取得的帳單數量 */
    fetched: number;
    /** 成功合併到本地的帳單數量（新帳單） */
    merged: number;
    /** 已存在但已更新的帳單數量 */
    updated: number;
    /** 已存在且無需更新的帳單數量（跳過） */
    skipped: number;
}

export function useMyBillsSync() {
    const [isFetching, setIsFetching] = useState(false);
    const [error, setError] = useState<SyncError | null>(null);

    const { importBillsFromRemote, bills, rebaseBillFromServer } = useSnapSplitStore();
    const { isAuthenticated } = useAuthStore();

    // 使用 React Query 的 hook，但設為 disabled，手動觸發
    const myBillsQuery = useGetMyBills({
        query: {
            enabled: false,
            staleTime: 30000, // 30 秒內不重新拉取
        },
    });

    /**
     * 從雲端拉取我的帳單（返回原始 DTO）
     */
    const fetchMyBillDtos = useCallback(async (): Promise<BillDto[]> => {
        if (!isAuthenticated) {
            return [];
        }

        setIsFetching(true);
        setError(null);

        try {
            const result = await myBillsQuery.refetch();

            if (!result.data?.success || !result.data.data) {
                throw new Error(result.data?.error?.message ?? '無法取得帳單列表');
            }

            return result.data.data;
        } catch (err) {
            const syncError: SyncError = {
                code: 'SERVER_ERROR',
                message: err instanceof Error ? err.message : '取得帳單失敗',
                retryable: true,
            };
            setError(syncError);
            throw err;
        } finally {
            setIsFetching(false);
        }
    }, [isAuthenticated, myBillsQuery]);

    /**
     * 從雲端拉取我的帳單（轉換為 Bill）
     */
    const fetchMyBills = useCallback(async (): Promise<Bill[]> => {
        const dtos = await fetchMyBillDtos();
        return dtos.map(dto => billDtoToBill(dto));
    }, [fetchMyBillDtos]);

    /**
     * 同步我的帳單（拉取、合併新帳單、更新版本較舊的帳單）
     */
    const syncMyBills = useCallback(async (): Promise<MyBillsSyncResult> => {
        console.log('[MyBillsSync] syncMyBills called, isAuthenticated:', isAuthenticated);

        if (!isAuthenticated) {
            console.log('[MyBillsSync] Not authenticated - aborting');
            return { fetched: 0, merged: 0, updated: 0, skipped: 0 };
        }

        try {
            // 取得原始 DTO（需要保留 DTO 用於 rebase）
            const remoteBillDtos = await fetchMyBillDtos();
            console.log('[MyBillsSync] Fetched remote bills:', remoteBillDtos.length, remoteBillDtos.map(b => ({ id: b.id, name: b.name, version: b.version })));

            if (remoteBillDtos.length === 0) {
                console.log('[MyBillsSync] No remote bills found');
                return { fetched: 0, merged: 0, updated: 0, skipped: 0 };
            }

            // 建立本地帳單的 remoteId -> bill 映射
            const localBillsByRemoteId = new Map<string, Bill>();
            for (const bill of bills) {
                if (bill.remoteId) {
                    localBillsByRemoteId.set(bill.remoteId, bill);
                }
            }

            const newBills: Bill[] = [];
            let updatedCount = 0;
            let skippedCount = 0;

            for (const dto of remoteBillDtos) {
                if (!dto.id) continue;

                const localBill = localBillsByRemoteId.get(dto.id);

                if (!localBill) {
                    // 新帳單：加入匯入清單
                    newBills.push(billDtoToBill(dto));
                    console.log('[MyBillsSync] New bill:', dto.id, dto.name);
                } else {
                    // 已存在：比較版本
                    const localVersion = localBill.version ?? 0;
                    const remoteVersion = dto.version ?? 0;

                    if (remoteVersion > localVersion) {
                        // Server 版本較新：執行 rebase
                        console.log('[MyBillsSync] Updating bill:', dto.id, `v${localVersion} -> v${remoteVersion}`);
                        rebaseBillFromServer(localBill.id, dto);
                        updatedCount++;
                    } else {
                        // 版本相同或本地較新：跳過
                        console.log('[MyBillsSync] Skipping bill:', dto.id, `(local v${localVersion}, remote v${remoteVersion})`);
                        skippedCount++;
                    }
                }
            }

            // 匯入新帳單
            const mergedCount = newBills.length > 0 ? importBillsFromRemote(newBills, 'merge') : 0;
            console.log('[MyBillsSync] Result:', { merged: mergedCount, updated: updatedCount, skipped: skippedCount });

            return {
                fetched: remoteBillDtos.length,
                merged: mergedCount,
                updated: updatedCount,
                skipped: skippedCount,
            };
        } catch (err) {
            console.error('[MyBillsSync] Error:', err);
            return { fetched: 0, merged: 0, updated: 0, skipped: 0 };
        }
    }, [isAuthenticated, fetchMyBillDtos, bills, importBillsFromRemote, rebaseBillFromServer]);

    /**
     * 強制刷新（替換模式）
     */
    const forceRefresh = useCallback(async (): Promise<MyBillsSyncResult> => {
        if (!isAuthenticated) {
            return { fetched: 0, merged: 0, updated: 0, skipped: 0 };
        }

        try {
            const remoteBills = await fetchMyBills();

            if (remoteBills.length === 0) {
                return { fetched: 0, merged: 0, updated: 0, skipped: 0 };
            }

            // 替換模式：用遠端帳單替換已同步的帳單
            const mergedCount = importBillsFromRemote(remoteBills, 'replace_list');

            return {
                fetched: remoteBills.length,
                merged: mergedCount,
                updated: 0,
                skipped: 0,
            };
        } catch {
            return { fetched: 0, merged: 0, updated: 0, skipped: 0 };
        }
    }, [isAuthenticated, fetchMyBills, importBillsFromRemote]);

    return {
        isFetching,
        error,
        fetchMyBills,
        syncMyBills,
        forceRefresh,
    };
}
