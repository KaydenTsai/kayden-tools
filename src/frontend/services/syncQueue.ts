import {
    syncBill,
    deleteBill,
} from '@/api';
import type { SyncBillResponseDtoApiResponse } from '@/api';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { billToSyncRequest } from '@/adapters/billAdapter'; // Import from adapter

interface SyncOperation {
    id: string;
    billId: string;
    type: 'upload' | 'delete';
    retryCount: number;
    maxRetries: number;
    createdAt: number;
    lastAttemptAt?: number;
    error?: string;
}

const STORAGE_KEY = 'kayden-tools-sync-queue';
const MAX_RETRIES = 3;
const RETRY_DELAY_MS = 5000;

/**
 * 同步佇列管理
 */
class SyncQueueService {
    private queue: SyncOperation[] = [];
    private isProcessing = false;
    private onlineListener: (() => void) | null = null;

    constructor() {
        this.loadFromStorage();
        this.setupOnlineListener();
    }

    private loadFromStorage() {
        try {
            const stored = localStorage.getItem(STORAGE_KEY);
            if (stored) {
                this.queue = JSON.parse(stored);
            }
        } catch {
            this.queue = [];
        }
    }

    private saveToStorage() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(this.queue));
        } catch {
            console.error('Failed to save sync queue to storage');
        }
    }

    private setupOnlineListener() {
        this.onlineListener = () => {
            if (navigator.onLine) {
                this.processQueue();
            }
        };
        window.addEventListener('online', this.onlineListener);
    }

    enqueue(billId: string, type: 'upload' | 'delete' = 'upload') {
        const existingIndex = this.queue.findIndex(
            op => op.billId === billId && op.type === type
        );

        if (existingIndex >= 0) {
            this.queue[existingIndex].retryCount = 0;
            this.queue[existingIndex].error = undefined;
        } else {
            const operation: SyncOperation = {
                id: crypto.randomUUID(),
                billId,
                type,
                retryCount: 0,
                maxRetries: MAX_RETRIES,
                createdAt: Date.now(),
            };
            this.queue.push(operation);
        }

        this.saveToStorage();
        this.processQueue();
    }

    dequeue(operationId: string) {
        this.queue = this.queue.filter(op => op.id !== operationId);
        this.saveToStorage();
    }

    async processQueue() {
        if (this.isProcessing || !navigator.onLine) return;

        this.isProcessing = true;

        const pendingOps = this.queue.filter(
            op => op.retryCount < op.maxRetries
        );

        for (const operation of pendingOps) {
            try {
                await this.executeOperation(operation);
                this.dequeue(operation.id);
            } catch (error) {
                operation.retryCount++;
                operation.lastAttemptAt = Date.now();
                operation.error = error instanceof Error ? error.message : 'Unknown error';

                if (operation.retryCount >= operation.maxRetries) {
                    const store = useSnapSplitStore.getState();
                    store.setBillSyncStatus(operation.billId, 'error', operation.error);
                }

                this.saveToStorage();

                await new Promise(resolve => setTimeout(resolve, RETRY_DELAY_MS));
            }
        }

        this.isProcessing = false;
    }

    private async executeOperation(operation: SyncOperation) {
        const store = useSnapSplitStore.getState();

        if (operation.type === 'upload') {
            const bill = store.bills.find(b => b.id === operation.billId);
            if (!bill) {
                throw new Error('Bill not found');
            }

            store.setBillSyncStatus(bill.id, 'syncing');

            const request = billToSyncRequest(bill);
            const response = await syncBill(request);
            const apiResponse = response as SyncBillResponseDtoApiResponse;

            if (!apiResponse.success || !apiResponse.data) {
                throw new Error(apiResponse.error?.message ?? 'Sync failed');
            }

            const syncResponse = apiResponse.data;
            store.setBillRemoteId(bill.id, syncResponse.remoteId!, syncResponse.shareCode ?? undefined);

            if (syncResponse.idMappings) {
                store.applyIdMappings(bill.id, {
                    members: syncResponse.idMappings.members ?? {},
                    expenses: syncResponse.idMappings.expenses ?? {},
                    expenseItems: syncResponse.idMappings.expenseItems ?? {},
                });
            }

            store.markBillAsSynced(bill.id);
        } else if (operation.type === 'delete') {
            const bill = store.bills.find(b => b.id === operation.billId);
            if (bill?.remoteId) {
                await deleteBill(bill.remoteId);
            }
        }
    }

    getQueueStatus() {
        return {
            pending: this.queue.filter(op => op.retryCount < op.maxRetries).length,
            failed: this.queue.filter(op => op.retryCount >= op.maxRetries).length,
            isProcessing: this.isProcessing,
        };
    }

    clearFailedOperations() {
        this.queue = this.queue.filter(op => op.retryCount < op.maxRetries);
        this.saveToStorage();
    }

    retryFailedOperations() {
        for (const op of this.queue) {
            if (op.retryCount >= op.maxRetries) {
                op.retryCount = 0;
                op.error = undefined;
            }
        }
        this.saveToStorage();
        this.processQueue();
    }

    destroy() {
        if (this.onlineListener) {
            window.removeEventListener('online', this.onlineListener);
        }
    }
}

export const syncQueue = new SyncQueueService();

/**
 * React Hook for sync queue status
 */
export function useSyncQueueStatus() {
    return syncQueue.getQueueStatus();
}
