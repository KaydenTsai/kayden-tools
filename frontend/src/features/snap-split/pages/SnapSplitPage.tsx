import {useEffect} from 'react';
import {useCurrentBill, useSnapSplitStore} from '@/features/snap-split/stores/snapSplitStore';
import {useAuthStore} from '@/stores/authStore';
import {syncLogger} from '@/shared/lib/logger';
import {BillListView} from './views/BillListView';
import {BillDetailView} from './views/BillDetailView';
import {useAutoSync} from '../hooks/useAutoSync';
import {useBillCollaboration} from '../hooks/useBillCollaboration';
import {useLoginHandshake} from '../hooks/useLoginHandshake';
import {SyncHandshakeDialog} from '../components/SyncHandshakeDialog';

export function SnapSplitPage() {
    const {selectBill} = useSnapSplitStore();
    const {isAuthenticated} = useAuthStore();
    const currentBill = useCurrentBill();

    // 自動同步 Hook
    const {pendingSyncCount, isSyncing} = useAutoSync({enabled: isAuthenticated});

    // SignalR 即時協作 Hook
    const {isConnected} = useBillCollaboration();

    // 登入握手 Hook
    const {
        showDialog: showSyncDialog,
        unsyncedBills,
        syncProgress,
        syncAllBills,
        syncSelectedBills,
        dismissDialog,
    } = useLoginHandshake();

    // Debug 日誌
    useEffect(() => {
        syncLogger.debug('Auth:', isAuthenticated, 'Connected:', isConnected, 'Pending:', pendingSyncCount);
    }, [isAuthenticated, isConnected, pendingSyncCount]);

    const handleBack = () => {
        selectBill('');
    };

    return (
        <div className="flex flex-col h-full">
            {/* 主要內容 */}
            <div className="flex-1 overflow-hidden">
                {currentBill ? (
                    <BillDetailView
                        bill={currentBill}
                        onBack={handleBack}
                        isAuthenticated={isAuthenticated}
                        isSyncing={isSyncing}
                        isConnected={isConnected}
                    />
                ) : (
                    <BillListView/>
                )}
            </div>

            {/* 登入握手同步對話框 */}
            <SyncHandshakeDialog
                open={showSyncDialog}
                onOpenChange={(open) => !open && dismissDialog()}
                unsyncedBills={unsyncedBills}
                syncProgress={syncProgress}
                onSyncAll={syncAllBills}
                onSyncSelected={syncSelectedBills}
                onDismiss={dismissDialog}
            />
        </div>
    );
}
