import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

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

export interface OperationRequest {
    clientId: string;
    billId: string;
    opType: string;
    targetId?: string;
    payload: any;
    baseVersion: number;
}

export interface OperationRejected {
    clientId: string;
    reason: string;
    currentVersion: number;
    missingOperations: Operation[];
}

/**
 * SendOperation 的返回結果
 * 後端現在返回結果而非廣播給發送者
 */
export interface OperationResult {
    success: boolean;
    operation?: Operation;
    rejected?: OperationRejected;
}

type OnOperationReceived = (op: Operation) => void;
type OnOperationRejected = (error: OperationRejected) => void;

class BillConnection {
    private connection: HubConnection | null = null;
    private connectionPromise: Promise<void> | null = null;
    private currentAccessToken: string | undefined = undefined;
    private handlers: {
        onOperationReceived?: OnOperationReceived;
        onOperationRejected?: OnOperationRejected;
    } = {};

    async connect(baseUrl: string, accessToken?: string): Promise<void> {
        // 如果 token 變更，需要重新連接
        if (this.connectionPromise && this.currentAccessToken !== accessToken) {
            console.log("[SignalR] Token changed, reconnecting...");
            await this.disconnect();
        }

        // 如果已經有連接或正在連接中，返回現有的 promise
        if (this.connectionPromise) return this.connectionPromise;

        this.currentAccessToken = accessToken;
        this.connectionPromise = this._doConnect(baseUrl, accessToken);
        return this.connectionPromise;
    }

    private async _doConnect(baseUrl: string, accessToken?: string): Promise<void> {
        try {
            this.connection = new HubConnectionBuilder()
                .withUrl(`${baseUrl}/hubs/bill`, {
                    accessTokenFactory: accessToken ? () => accessToken : undefined,
                })
                .withAutomaticReconnect()
                .configureLogging(LogLevel.Information)
                .build();

            this.connection.on("OperationReceived", (op: Operation) => {
                console.log("[SignalR] Received operation:", op.opType, op.targetId);
                this.handlers.onOperationReceived?.(op);
            });

            this.connection.on("OperationRejected", (error: OperationRejected) => {
                console.warn("[SignalR] Operation rejected:", error.reason);
                this.handlers.onOperationRejected?.(error);
            });

            this.connection.onclose((error) => {
                console.warn("[SignalR] Connection closed:", error);
                this.connectionPromise = null;
            });

            await this.connection.start();
            console.log("[SignalR] Connected to BillHub");
        } catch (error) {
            console.error("[SignalR] Failed to connect:", error);
            this.connection = null;
            this.connectionPromise = null;
            throw error;
        }
    }

    isConnected(): boolean {
        return this.connection?.state === "Connected";
    }

    async joinBill(billId: string): Promise<void> {
        if (!this.connection) return;
        await this.connection.invoke("JoinBill", billId);
    }

    async leaveBill(billId: string): Promise<void> {
        if (!this.connection) return;
        await this.connection.invoke("LeaveBill", billId);
    }

    async sendOperation(request: OperationRequest): Promise<OperationResult> {
        if (!this.connection || this.connection.state !== "Connected") {
            throw new Error("SignalR not connected");
        }
        console.log("[SignalR] Sending operation:", request.opType, request.targetId);
        const result = await this.connection.invoke<OperationResult>("SendOperation", request);
        if (result.success) {
            console.log("[SignalR] Operation confirmed, new version:", result.operation?.version);
        } else {
            console.warn("[SignalR] Operation rejected:", result.rejected?.reason);
        }
        return result;
    }

    setHandlers(handlers: typeof this.handlers) {
        this.handlers = { ...this.handlers, ...handlers };
    }

    async disconnect(): Promise<void> {
        if (!this.connection) return;
        await this.connection.stop();
        this.connection = null;
        this.connectionPromise = null;
        this.currentAccessToken = undefined;
    }
}

export const billConnection = new BillConnection();
