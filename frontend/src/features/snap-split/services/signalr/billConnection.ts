import {HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel} from "@microsoft/signalr";
import {signalRLogger} from "@/shared/lib/logger";

/**
 * 本地操作介面 (僅供本地狀態套用時參考型別)
 */
export interface Operation {
    id: string;
    billId: string;
    version: number;
    opType: string;
    targetId?: string;
    payload: any;
    createdByUserId?: string;
    clientId: string;
    createdAt: string;
}

/**
 * 伺服器更新通知訊息
 */
export interface BillUpdatedMessage {
    billId: string;
    newVersion: number;
    updatedBy?: string;
}

/** 連線狀態 */
export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

type OnBillUpdated = (message: BillUpdatedMessage) => void;
type OnConnectionStatusChange = (status: ConnectionStatus) => void;

class BillConnection {
    private connection: HubConnection | null = null;
    private connectionPromise: Promise<void> | null = null;
    private currentAccessToken: string | undefined = undefined;
    private joinedBillIds: Set<string> = new Set();
    private handlers: {
        onBillUpdated?: OnBillUpdated;
        onConnectionStatusChange?: OnConnectionStatusChange;
    } = {};

    async connect(baseUrl: string, accessToken?: string): Promise<void> {
        if (this.connectionPromise && this.currentAccessToken !== accessToken) {
            signalRLogger.debug("Token changed, reconnecting...");
            await this.disconnect();
        }

        if (this.connectionPromise) return this.connectionPromise;

        this.currentAccessToken = accessToken;
        this.connectionPromise = this._doConnect(baseUrl, accessToken);
        return this.connectionPromise;
    }

    private async _doConnect(baseUrl: string, accessToken?: string): Promise<void> {
        try {
            this.notifyStatusChange('connecting');

            this.connection = new HubConnectionBuilder()
                .withUrl(`${baseUrl}/hubs/bill`, {
                    accessTokenFactory: accessToken ? () => accessToken : undefined,
                })
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // 重連間隔
                .configureLogging(LogLevel.Information)
                .build();

            // 監聽更新通知
            this.connection.on("BillUpdated", (message: BillUpdatedMessage) => {
                signalRLogger.debug("BillUpdated received:", message);
                if (this.handlers.onBillUpdated) {
                    this.handlers.onBillUpdated(message);
                } else {
                    signalRLogger.warn("No onBillUpdated handler registered");
                }
            });

            // 連線關閉事件
            this.connection.onclose((error) => {
                signalRLogger.warn("Connection closed:", error);
                this.connectionPromise = null;
                this.notifyStatusChange('disconnected');
            });

            // 重連事件
            this.connection.onreconnecting((error) => {
                signalRLogger.debug("Reconnecting...", error);
                this.notifyStatusChange('reconnecting');
            });

            // 重連成功事件
            this.connection.onreconnected(async (connectionId) => {
                signalRLogger.info("Reconnected:", connectionId);
                this.notifyStatusChange('connected');
                // 重新加入之前的帳單房間
                await this.rejoinBills();
            });

            await this.connection.start();
            signalRLogger.info("Connected to BillHub");
            this.notifyStatusChange('connected');
        } catch (error) {
            signalRLogger.error("Failed to connect:", error);
            this.connection = null;
            this.connectionPromise = null;
            this.notifyStatusChange('disconnected');
            throw error;
        }
    }

    private notifyStatusChange(status: ConnectionStatus): void {
        this.handlers.onConnectionStatusChange?.(status);
    }

    private async rejoinBills(): Promise<void> {
        if (!this.connection) return;
        for (const billId of this.joinedBillIds) {
            try {
                await this.connection.invoke("JoinBill", billId);
                signalRLogger.debug(`Rejoined bill: ${billId}`);
            } catch (error) {
                signalRLogger.error(`Failed to rejoin bill ${billId}:`, error);
            }
        }
    }

    isConnected(): boolean {
        return this.connection?.state === HubConnectionState.Connected;
    }

    getConnectionState(): ConnectionStatus {
        if (!this.connection) return 'disconnected';
        switch (this.connection.state) {
            case HubConnectionState.Connected:
                return 'connected';
            case HubConnectionState.Connecting:
            case HubConnectionState.Reconnecting:
                return 'connecting';
            default:
                return 'disconnected';
        }
    }

    async joinBill(billId: string): Promise<void> {
        if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
            signalRLogger.warn('Cannot join bill - not connected');
            return;
        }
        try {
            await this.connection.invoke("JoinBill", billId);
            this.joinedBillIds.add(billId);
            signalRLogger.debug(`Joined bill: ${billId}`);
        } catch (error) {
            signalRLogger.error(`Failed to join bill ${billId}:`, error);
            throw error;
        }
    }

    async leaveBill(billId: string): Promise<void> {
        if (!this.connection) return;
        try {
            await this.connection.invoke("LeaveBill", billId);
            this.joinedBillIds.delete(billId);
            signalRLogger.debug(`Left bill: ${billId}`);
        } catch (error) {
            signalRLogger.error(`Failed to leave bill ${billId}:`, error);
        }
    }

    setHandlers(handlers: typeof this.handlers) {
        this.handlers = {...this.handlers, ...handlers};
    }

    async disconnect(): Promise<void> {
        if (!this.connection) return;
        this.joinedBillIds.clear();
        await this.connection.stop();
        this.connection = null;
        this.connectionPromise = null;
        this.currentAccessToken = undefined;
        this.notifyStatusChange('disconnected');
    }

    /** 取得已加入的帳單 IDs */
    getJoinedBillIds(): string[] {
        return Array.from(this.joinedBillIds);
    }
}

export const billConnection = new BillConnection();