import { useState, useEffect, useCallback } from 'react';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { useBatchSync } from './useBillSync';
import { useMyBillsSync } from './useMyBillsSync';
import type { Bill } from '@/types/snap-split';

interface HandshakeState {
    isChecking: boolean;
    isFetchingRemote: boolean;
    unsyncedBills: Bill[];
    showDialog: boolean;
    syncProgress: {
        total: number;
        completed: number;
        failed: number;
    };
    remoteSyncResult: {
        fetched: number;
        merged: number;
    } | null;
}

/**
 * 登入握手 Hook
 * 偵測本地未同步帳單並提供同步對話框控制
 */
export function useLoginHandshake() {
    const { getUnsyncedBills } = useSnapSplitStore();
    const { syncAllUnsynced } = useBatchSync();
    const { syncMyBills } = useMyBillsSync();

    const [state, setState] = useState<HandshakeState>({
        isChecking: false,
        isFetchingRemote: false,
        unsyncedBills: [],
        showDialog: false,
        syncProgress: { total: 0, completed: 0, failed: 0 },
        remoteSyncResult: null,
    });

    /**
     * 完整登入握手流程：
     * 1. 從雲端拉取使用者參與的帳單
     * 2. 合併到本地
     * 3. 檢查本地未同步帳單
     */
    const performHandshake = useCallback(async () => {
        setState(prev => ({ ...prev, isChecking: true, isFetchingRemote: true }));

        // Step 1: 從雲端拉取帳單
        try {
            const result = await syncMyBills();
            setState(prev => ({
                ...prev,
                isFetchingRemote: false,
                remoteSyncResult: result,
            }));
        } catch (err) {
            console.warn('Failed to fetch remote bills:', err);
            setState(prev => ({ ...prev, isFetchingRemote: false }));
        }

        // Step 2: 檢查本地未同步帳單
        const bills = getUnsyncedBills();
        setState(prev => ({
            ...prev,
            isChecking: false,
            unsyncedBills: bills,
            showDialog: bills.length > 0,
        }));

        return bills;
    }, [syncMyBills, getUnsyncedBills]);

    const checkUnsyncedBills = useCallback(() => {
        setState(prev => ({ ...prev, isChecking: true }));
        const bills = getUnsyncedBills();
        setState(prev => ({
            ...prev,
            isChecking: false,
            unsyncedBills: bills,
            showDialog: bills.length > 0,
        }));

        return bills;
    }, [getUnsyncedBills]);

    const dismissDialog = useCallback(() => {
        setState(prev => ({ ...prev, showDialog: false }));
    }, []);

    const syncSelectedBills = useCallback(async (billIds: string[]) => {
        const billsToSync = state.unsyncedBills.filter(b => billIds.includes(b.id));
        if (billsToSync.length === 0) {
            dismissDialog();

            return;
        }

        setState(prev => ({
            ...prev,
            syncProgress: { total: billsToSync.length, completed: 0, failed: 0 },
        }));

        const results = await syncAllUnsynced();

        const completed = results.filter(r => r.success).length;
        const failed = results.filter(r => !r.success).length;

        setState(prev => ({
            ...prev,
            syncProgress: { total: billsToSync.length, completed, failed },
            showDialog: failed > 0,
            unsyncedBills: failed > 0 ? getUnsyncedBills() : [],
        }));
    }, [state.unsyncedBills, syncAllUnsynced, dismissDialog, getUnsyncedBills]);

    const syncAllBills = useCallback(async () => {
        const billIds = state.unsyncedBills.map(b => b.id);
        await syncSelectedBills(billIds);
    }, [state.unsyncedBills, syncSelectedBills]);

    return {
        ...state,
        performHandshake,
        checkUnsyncedBills,
        dismissDialog,
        syncSelectedBills,
        syncAllBills,
    };
}

/**
 * 監聽登入狀態變化並觸發握手
 * 登入時會自動：
 * 1. 從雲端拉取使用者參與的帳單
 * 2. 合併到本地
 * 3. 檢查並提示未同步的本地帳單
 */
export function useAuthHandshake(isAuthenticated: boolean) {
    const handshake = useLoginHandshake();

    useEffect(() => {
        if (isAuthenticated) {
            // 使用完整握手流程（包含雲端拉取）
            handshake.performHandshake();
        }
    }, [isAuthenticated]);

    return handshake;
}
