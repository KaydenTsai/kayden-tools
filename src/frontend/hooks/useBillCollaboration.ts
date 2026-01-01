import { useEffect, useRef } from "react";
import { useSnapSplitStore } from "@/stores/snapSplitStore";
import { billConnection, type OperationRejected } from "@/services/signalr/billConnection";
import { useAuthStore } from "@/stores/authStore";

interface UseBillCollaborationOptions {
    /** 後端帳單 ID (remoteId) */
    remoteId: string | undefined;
    /** 本地帳單 ID */
    localBillId: string | undefined;
    /** 是否啟用協作 */
    enabled?: boolean;
}

export function useBillCollaboration({ remoteId, localBillId, enabled = true }: UseBillCollaborationOptions) {
    const accessToken = useAuthStore(state => state.accessToken);
    const joinedBillRef = useRef<string | null>(null);

    useEffect(() => {
        if (!remoteId || !localBillId || !enabled) {
            return;
        }

        const { setConnectionStatus } = useSnapSplitStore.getState();

        /**
         * Rebase 邏輯：當收到 OperationRejected 時處理版本衝突
         */
        const handleOperationRejected = (rejection: OperationRejected) => {
            console.warn("[Collaboration] Operation rejected:", rejection.reason);
            console.log(`[Collaboration] Current server version: ${rejection.currentVersion}`);
            console.log(`[Collaboration] Missing ${rejection.missingOperations.length} operations`);

            const sortedOps = [...rejection.missingOperations].sort((a, b) => a.version - b.version);
            for (const op of sortedOps) {
                console.log(`[Collaboration] Applying missing op: ${op.opType} v${op.version}`);
                useSnapSplitStore.getState().applyOperation(op);
            }
        };

        const setup = async () => {
            try {
                setConnectionStatus('connecting');

                // 1. 建立連線 (使用與 API 相同的 base URL)
                const baseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5063';
                await billConnection.connect(baseUrl, accessToken ?? undefined);

                // 2. 加入房間 (使用後端的 remoteId)
                await billConnection.joinBill(remoteId);
                joinedBillRef.current = remoteId;

                // 3. 註冊處理器
                billConnection.setHandlers({
                    onOperationReceived: (op) => {
                        // 確保操作套用到正確的帳單
                        if (op.billId === remoteId) {
                            useSnapSplitStore.getState().applyOperation(op);
                        }
                    },
                    onOperationRejected: handleOperationRejected,
                });

                useSnapSplitStore.getState().setConnectionStatus('connected');
                console.log(`[Collaboration] Connected to bill ${remoteId}`);
            } catch (error) {
                console.error("[Collaboration] Setup failed:", error);
                useSnapSplitStore.getState().setConnectionStatus('disconnected');
            }
        };

        setup();

        return () => {
            // 離開房間並重設連線狀態
            if (joinedBillRef.current) {
                billConnection.leaveBill?.(joinedBillRef.current);
                joinedBillRef.current = null;
            }
            useSnapSplitStore.getState().setConnectionStatus('disconnected');
        };
    }, [remoteId, localBillId, enabled, accessToken]);

    return {
        isConnected: useSnapSplitStore.getState().connectionStatus === 'connected',
    };
}
