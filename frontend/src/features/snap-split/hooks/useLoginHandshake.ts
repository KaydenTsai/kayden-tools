/**
 * useLoginHandshake - 登入握手 Hook
 *
 * 功能：
 * 1. 登入後檢查本地未同步帳單
 * 2. 顯示對話框讓用戶選擇是否同步
 * 3. 拉取用戶雲端帳單並合併
 */

import { useState, useCallback, useEffect, useRef } from 'react';
import { useSnapSplitStore } from '../stores/snapSplitStore';
import { useBillSync } from './useBillSync';
import { useMyBillsSync } from './useMyBillsSync';
import { useAuthStore } from '@/stores/authStore';
import type { Bill } from '../types/snap-split';

/**
 * 登入握手開關
 * - Step 1: 後端冪等性已修復（LocalClientId + 唯一索引）
 * - Step 2: 前端並發控制已加入（syncLocks）
 */
const TEMP_DISABLE_LOGIN_HANDSHAKE = false;

interface SyncProgress {
    total: number;
    completed: number;
    failed: number;
}

interface RemoteSyncResult {
    fetched: number;
    merged: number;
}

interface HandshakeResult {
    localSynced: number;
    localFailed: number;
    remoteFetched: number;
    remoteMerged: number;
}

export function useLoginHandshake() {
    const [isChecking, setIsChecking] = useState(false);
    const [isFetchingRemote, setIsFetchingRemote] = useState(false);
    const [unsyncedBills, setUnsyncedBills] = useState<Bill[]>([]);
    const [showDialog, setShowDialog] = useState(false);
    const [syncProgress, setSyncProgress] = useState<SyncProgress>({
        total: 0,
        completed: 0,
        failed: 0,
    });
    const [remoteSyncResult, setRemoteSyncResult] = useState<RemoteSyncResult | null>(null);

    const { getUnsyncedBills } = useSnapSplitStore();
    const { syncBill } = useBillSync();
    const { syncMyBills } = useMyBillsSync();
    const { isAuthenticated, user, isHydrated } = useAuthStore();

    /**
     * 檢查未同步的帳單
     */
    const checkUnsyncedBills = useCallback((): Bill[] => {
        const bills = getUnsyncedBills();
        // 過濾出純本地帳單（沒有 remoteId 的）
        const localOnlyBills = bills.filter(b => !b.remoteId && b.syncStatus === 'local');
        setUnsyncedBills(localOnlyBills);
        return localOnlyBills;
    }, [getUnsyncedBills]);

    /**
     * 執行完整握手流程
     */
    const performHandshake = useCallback(async (): Promise<HandshakeResult> => {
        if (!isAuthenticated) {
            return { localSynced: 0, localFailed: 0, remoteFetched: 0, remoteMerged: 0 };
        }

        setIsChecking(true);
        const result: HandshakeResult = {
            localSynced: 0,
            localFailed: 0,
            remoteFetched: 0,
            remoteMerged: 0,
        };

        try {
            // Step 1: 拉取雲端帳單
            setIsFetchingRemote(true);
            const remoteResult = await syncMyBills();
            result.remoteFetched = remoteResult.fetched;
            result.remoteMerged = remoteResult.merged;
            setRemoteSyncResult(remoteResult);
            setIsFetchingRemote(false);

            // Step 2: 檢查本地未同步帳單
            const localBills = checkUnsyncedBills();

            if (localBills.length > 0) {
                // 有未同步帳單，顯示對話框讓用戶選擇
                setShowDialog(true);
            }

            return result;
        } catch {
            return result;
        } finally {
            setIsChecking(false);
        }
    }, [isAuthenticated, syncMyBills, checkUnsyncedBills]);

    /**
     * 同步選定的帳單
     */
    const syncSelectedBills = useCallback(async (selectedBillIds: string[]): Promise<void> => {
        const billsToSync = unsyncedBills.filter(b => selectedBillIds.includes(b.id));
        if (billsToSync.length === 0) {
            setShowDialog(false);
            return;
        }

        setSyncProgress({ total: billsToSync.length, completed: 0, failed: 0 });

        for (const bill of billsToSync) {
            const result = await syncBill(bill);

            setSyncProgress(prev => ({
                ...prev,
                completed: prev.completed + 1,
                failed: result.success ? prev.failed : prev.failed + 1,
            }));
        }

        // 更新未同步列表
        checkUnsyncedBills();
        setShowDialog(false);
    }, [unsyncedBills, syncBill, checkUnsyncedBills]);

    /**
     * 同步所有未同步帳單
     */
    const syncAllBills = useCallback(async (): Promise<void> => {
        await syncSelectedBills(unsyncedBills.map(b => b.id));
    }, [unsyncedBills, syncSelectedBills]);

    /**
     * 關閉對話框（稍後再說）
     */
    const dismissDialog = useCallback(() => {
        setShowDialog(false);
    }, []);

    // 追蹤已執行過的用戶 ID（防止 StrictMode 雙重執行）
    const handshakeExecutedForUserRef = useRef<string | null>(null);

    /**
     * 監聽登入狀態變化（等待 hydration 完成）
     */
    useEffect(() => {
        console.log('[LoginHandshake] useEffect triggered:', {
            isHydrated,
            isAuthenticated,
            userId: user?.id,
            handshakeExecutedFor: handshakeExecutedForUserRef.current,
            TEMP_DISABLE: TEMP_DISABLE_LOGIN_HANDSHAKE,
        });

        // ⚠️ 臨時停用登入握手
        if (TEMP_DISABLE_LOGIN_HANDSHAKE) {
            console.log('[LoginHandshake] DISABLED - skipping');
            return;
        }

        // 1. 如果還沒還原完成，什麼都不做（等待下一次 render）
        if (!isHydrated) {
            console.log('[LoginHandshake] Not hydrated yet - waiting');
            return;
        }

        // 2. 還原完成了，但使用者沒登入 -> 重置並放棄
        if (!isAuthenticated || !user) {
            console.log('[LoginHandshake] Not authenticated - resetting ref');
            handshakeExecutedForUserRef.current = null;
            return;
        }

        // 3. 防止同一用戶的重複執行（StrictMode 會觸發兩次）
        if (handshakeExecutedForUserRef.current === user.id) {
            console.log('[LoginHandshake] Already executed for this user - skipping');
            return;
        }
        handshakeExecutedForUserRef.current = user.id;

        // 4. 還原完成且已登入 -> 執行握手
        console.log('[LoginHandshake] ✅ Starting handshake for user:', user.id);
        performHandshake();
    }, [isHydrated, isAuthenticated, user?.id, performHandshake]);

    return {
        isChecking,
        isFetchingRemote,
        unsyncedBills,
        showDialog,
        syncProgress,
        remoteSyncResult,
        performHandshake,
        checkUnsyncedBills,
        dismissDialog,
        syncSelectedBills,
        syncAllBills,
        /** Store hydration 完成狀態 */
        isHydrated,
    };
}

// Legacy alias
export function useAuthHandshake() {
    return useLoginHandshake();
}
