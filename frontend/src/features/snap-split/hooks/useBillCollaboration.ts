/**
 * useBillCollaboration - 即時協作 Hook
 *
 * 功能：
 * 1. 建立 SignalR 連接
 * 2. 加入/離開帳單房間
 * 3. 接收 BillUpdated 事件，自動拉取最新版本
 * 4. 處理重連
 */

import { useEffect, useState, useCallback, useRef } from 'react';
import { useSnapSplitStore, useCurrentBill } from '../stores/snapSplitStore';
import { billConnection, type BillUpdatedMessage, type ConnectionStatus } from '../services/signalr/billConnection';
import { getBillById } from '@/api/endpoints/bills/bills';
import { useAuthStore } from '@/stores/authStore';
import { collaborationLogger } from '@/shared/lib/logger';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5063';

interface CollaborationState {
    /** 連線狀態 */
    connectionStatus: ConnectionStatus;
    /** 是否已連線 */
    isConnected: boolean;
    /** 最後收到更新的時間 */
    lastUpdateAt: Date | null;
    /** 是否正在拉取更新 */
    isFetchingUpdate: boolean;
}

export function useBillCollaboration() {
    const [state, setState] = useState<CollaborationState>({
        connectionStatus: 'disconnected',
        isConnected: false,
        lastUpdateAt: null,
        isFetchingUpdate: false,
    });

    const currentBill = useCurrentBill();
    const { setConnectionStatus } = useSnapSplitStore();
    const { isAuthenticated, accessToken } = useAuthStore();

    const currentBillIdRef = useRef<string | null>(null);
    const isConnectingRef = useRef(false);

    /**
     * 處理 BillUpdated 事件
     * 注意：直接從 store 讀取最新狀態，避免 ref 延遲問題
     */
    const handleBillUpdated = useCallback(async (message: BillUpdatedMessage) => {
        console.log('[Collaboration] BillUpdated received:', message);

        // 直接從 store 讀取最新狀態（避免 ref 更新延遲）
        const currentBills = useSnapSplitStore.getState().bills;

        // 找到對應的本地帳單
        const localBill = currentBills.find(b => b.remoteId === message.billId);
        if (!localBill) {
            console.log('[Collaboration] Bill not found locally:', message.billId);
            return;
        }

        console.log('[Collaboration] Local bill state:', {
            localId: localBill.id,
            remoteId: localBill.remoteId,
            localVersion: localBill.version,
            remoteVersion: message.newVersion,
            syncStatus: localBill.syncStatus,
        });

        // 如果正在同步中，跳過（避免覆蓋自己的變更）
        if (localBill.syncStatus === 'syncing') {
            console.log('[Collaboration] Bill is syncing, skipping notification');
            return;
        }

        // BUG-002 修復：如果有本地變更，跳過 rebase（避免覆蓋本地變更）
        // 本地變更會在下次同步時透過 delta sync 合併到 Server
        if (localBill.syncStatus === 'modified') {
            collaborationLogger.warn(
                `[Collaboration] Bill has local modifications, skipping rebase. ` +
                `Local version: ${localBill.version}, Remote version: ${message.newVersion}. ` +
                `Changes will be merged on next sync.`
            );
            // 更新版本號以便下次同步時偵測衝突
            // 注意：不更新本地狀態，讓同步時處理合併
            return;
        }

        // 檢查版本：如果本地版本 >= 遠端版本，忽略
        if (localBill.version >= message.newVersion) {
            console.log(`[Collaboration] Local version ${localBill.version} >= remote ${message.newVersion}, ignoring`);
            return;
        }

        console.log(`[Collaboration] Fetching update: local v${localBill.version} -> remote v${message.newVersion}`);

        // 拉取最新版本
        setState(prev => ({ ...prev, isFetchingUpdate: true }));

        try {
            const response = await getBillById(message.billId);
            if (response.success && response.data) {
                // 使用 Server 資料重建本地狀態
                useSnapSplitStore.getState().rebaseBillFromServer(localBill.id, response.data);
                setState(prev => ({
                    ...prev,
                    lastUpdateAt: new Date(),
                    isFetchingUpdate: false,
                }));
                collaborationLogger.info('Bill updated from server successfully');
            }
        } catch (error) {
            collaborationLogger.error('Failed to fetch updated bill:', error);
            setState(prev => ({ ...prev, isFetchingUpdate: false }));
        }
    }, []); // 無依賴，使用 ref 讀取最新值

    /**
     * 處理連線狀態變化
     */
    const handleConnectionStatusChange = useCallback((status: ConnectionStatus) => {
        setState(prev => ({
            ...prev,
            connectionStatus: status,
            isConnected: status === 'connected',
        }));
        setConnectionStatus(status === 'connected' ? 'connected' : 'disconnected');
    }, [setConnectionStatus]);

    /**
     * 連接到 SignalR Hub
     */
    const connect = useCallback(async () => {
        if (!isAuthenticated) return;

        // 無論是否正在連線，都要設定最新的 handlers（修復 cleanup 後 handlers 遺失問題）
        billConnection.setHandlers({
            onBillUpdated: handleBillUpdated,
            onConnectionStatusChange: handleConnectionStatusChange,
        });

        // 如果正在連線中，只更新 handlers，不重複連線
        if (isConnectingRef.current) {
            collaborationLogger.debug('Already connecting, handlers updated');
            return;
        }

        isConnectingRef.current = true;

        try {
            // 建立連接
            await billConnection.connect(API_BASE_URL, accessToken ?? undefined);

            // 連線完成後，同步當前狀態（修復已連線但狀態未更新的問題）
            const currentStatus = billConnection.getConnectionState();
            if (currentStatus === 'connected') {
                handleConnectionStatusChange(currentStatus);
            }
        } catch (error) {
            collaborationLogger.error('Connection failed:', error);
        } finally {
            isConnectingRef.current = false;
        }
    }, [isAuthenticated, accessToken, handleBillUpdated, handleConnectionStatusChange]);

    /**
     * 加入帳單房間
     */
    const joinBill = useCallback(async (billId: string) => {
        if (!billConnection.isConnected()) return;

        try {
            await billConnection.joinBill(billId);
        } catch (error) {
            collaborationLogger.error('Failed to join bill:', error);
        }
    }, []);

    /**
     * 離開帳單房間
     */
    const leaveBill = useCallback(async (billId: string) => {
        if (!billConnection.isConnected()) return;

        try {
            await billConnection.leaveBill(billId);
        } catch (error) {
            collaborationLogger.error('Failed to leave bill:', error);
        }
    }, []);

    /**
     * 監聽登入狀態，登入時連接
     */
    useEffect(() => {
        if (isAuthenticated && accessToken) {
            connect();
        } else {
            billConnection.disconnect();
        }

        return () => {
            // 清除 handlers 但保持連接（避免記憶體洩漏）
            billConnection.setHandlers({
                onBillUpdated: undefined,
                onConnectionStatusChange: undefined,
            });
        };
    }, [isAuthenticated, accessToken, connect]);

    // 追蹤上次連線狀態，用於偵測重連
    const wasConnectedRef = useRef(false);

    /**
     * 監聽當前帳單變化，自動加入/離開房間
     * 注意：依賴 state.isConnected 確保連線建立後重新加入
     */
    useEffect(() => {
        const wasConnected = wasConnectedRef.current;
        wasConnectedRef.current = state.isConnected;

        // 連線尚未建立時，不執行任何操作
        if (!state.isConnected) return;

        const newBillId = currentBill?.remoteId;
        const prevBillId = currentBillIdRef.current;

        // 偵測重連：如果剛從斷線恢復，強制重新加入
        const isReconnect = !wasConnected && state.isConnected;

        // 如果帳單沒有變化且非重連，不執行操作
        if (newBillId === prevBillId && !isReconnect) return;

        // 離開之前的房間（僅在帳單變化時）
        if (prevBillId && newBillId !== prevBillId) {
            leaveBill(prevBillId);
        }

        // 加入新房間（帳單變化或重連時）
        if (newBillId) {
            collaborationLogger.debug('Joining room:', newBillId, isReconnect ? '(reconnect)' : '');
            joinBill(newBillId);
        }

        currentBillIdRef.current = newBillId ?? null;
    }, [currentBill?.remoteId, state.isConnected, joinBill, leaveBill]);

    /**
     * 組件卸載時離開當前房間
     */
    useEffect(() => {
        return () => {
            if (currentBillIdRef.current) {
                leaveBill(currentBillIdRef.current);
            }
        };
    }, [leaveBill]);

    return {
        ...state,
        connect,
        joinBill,
        leaveBill,
    };
}
