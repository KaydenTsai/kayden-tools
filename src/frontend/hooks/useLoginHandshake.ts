import { useState, useEffect, useCallback } from 'react';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { useBatchSync } from './useBillSync';
import type { Bill } from '@/types/snap-split';

interface HandshakeState {
    isChecking: boolean;
    unsyncedBills: Bill[];
    showDialog: boolean;
    syncProgress: {
        total: number;
        completed: number;
        failed: number;
    };
}

/**
 * 登入握手 Hook
 * 偵測本地未同步帳單並提供同步對話框控制
 */
export function useLoginHandshake() {
    const { getUnsyncedBills } = useSnapSplitStore();
    const { syncAllUnsynced } = useBatchSync();

    const [state, setState] = useState<HandshakeState>({
        isChecking: false,
        unsyncedBills: [],
        showDialog: false,
        syncProgress: { total: 0, completed: 0, failed: 0 },
    });

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
        checkUnsyncedBills,
        dismissDialog,
        syncSelectedBills,
        syncAllBills,
    };
}

/**
 * 監聽登入狀態變化並觸發握手
 */
export function useAuthHandshake(isAuthenticated: boolean) {
    const handshake = useLoginHandshake();

    useEffect(() => {
        if (isAuthenticated) {
            handshake.checkUnsyncedBills();
        }
    }, [isAuthenticated]);

    return handshake;
}
