import { useMutation } from '@tanstack/react-query';
import {
    getBillByShareCode,
    syncBill,
    deltaSyncBill,
} from '@/api';
import type {
    BillDto,
    BillDtoApiResponse,
    SyncBillResponseDto,
    SyncBillResponseDtoApiResponse,
    DeltaSyncResponse,
    DeltaSyncResponseApiResponse,
} from '@/api';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import {
    billDtoToBill,
    billToSyncRequest,
} from '@/adapters/billAdapter';
import { createDeltaRequest } from '@/services/sync/deltaFactory';
import type { Bill } from '@/types/snap-split';

/**
 * 從伺服器下載帳單（使用生成的 API）
 */
async function downloadBill(shareCode: string): Promise<BillDto> {
    const response = await getBillByShareCode(shareCode);
    const apiResponse = response as BillDtoApiResponse;

    if (!apiResponse.success || !apiResponse.data) {
        throw new Error(apiResponse.error?.message ?? 'Download failed');
    }

    return apiResponse.data;
}

/**
 * 上傳帳單到伺服器（使用舊版 Sync API，用於首次同步）
 */
async function uploadBill(bill: Bill): Promise<SyncBillResponseDto> {
    const request = billToSyncRequest(bill);
    const response = await syncBill(request);
    const apiResponse = response as SyncBillResponseDtoApiResponse;

    if (!apiResponse.success || !apiResponse.data) {
        throw new Error(apiResponse.error?.message ?? 'Sync failed');
    }

    return apiResponse.data;
}

/**
 * 上傳帳單（Delta Sync，用於後續更新）
 */
async function deltaSync(bill: Bill): Promise<DeltaSyncResponse> {
    if (!bill.remoteId) {
        throw new Error("Cannot use Delta Sync without remoteId");
    }
    const request = createDeltaRequest(bill);
    const response = await deltaSyncBill(bill.remoteId, request);
    const apiResponse = response as DeltaSyncResponseApiResponse;

    if (!apiResponse.success || !apiResponse.data) {
        throw new Error(apiResponse.error?.message ?? 'Delta Sync failed');
    }

    return apiResponse.data;
}

function cleanIdMap(map: Record<string, string | null> | null | undefined): Record<string, string> {
    if (!map) return {};
    const result: Record<string, string> = {};
    for (const [key, value] of Object.entries(map)) {
        if (value) {
            result[key] = value;
        }
    }
    return result;
}

/**
 * 帳單同步 Hook
 */
export function useBillSync() {
    const {
        setBillSyncStatus,
        setBillRemoteId,
        applyIdMappings,
        markBillAsSynced,
        importBill,
        importBillsFromRemote,
    } = useSnapSplitStore();

    // 1. Full Sync Mutation (首次同步)
    const uploadMutation = useMutation({
        mutationFn: uploadBill,
        onMutate: (bill) => {
            setBillSyncStatus(bill.id, 'syncing');
            return { sentUpdatedAt: bill.updatedAt };
        },
        onSuccess: (response, bill, context) => {
            if (response.latestBill) {
                if (import.meta.env.DEV) {
                    console.warn(
                        `[Sync] ⚠️ Concurrent conflict detected! Local v${bill.version} → Server v${response.version}. Applying server state.`
                    );
                }
                const remoteBill = billDtoToBill(response.latestBill, 'synced');
                importBillsFromRemote([remoteBill]);
                return;
            }

            if (import.meta.env.DEV) {
                console.log(`[Sync] Upload success for ${bill.name}. New version: ${response.version}`);
            }
            setBillRemoteId(bill.id, response.remoteId!, response.shareCode ?? undefined, response.version);

            if (response.idMappings) {
                applyIdMappings(bill.id, {
                    members: response.idMappings.members ?? {},
                    expenses: response.idMappings.expenses ?? {},
                    expenseItems: response.idMappings.expenseItems ?? {},
                });
            }

            markBillAsSynced(bill.id, context?.sentUpdatedAt);
        },
        onError: (error, bill) => {
            const message = error instanceof Error ? error.message : 'Sync failed';
            console.error('[Sync] Error:', message);
            setBillSyncStatus(bill.id, 'error', message);
        },
    });

    // 2. Delta Sync Mutation (後續更新)
    const deltaMutation = useMutation({
        mutationFn: deltaSync,
        onMutate: (bill) => {
            setBillSyncStatus(bill.id, 'syncing');
            return { sentUpdatedAt: bill.updatedAt };
        },
        onSuccess: (response, bill, context) => {
            if (response.mergedBill) {
                // 衝突或後端執行了合併，回傳最新狀態
                if (import.meta.env.DEV) {
                    console.warn(
                        `[DeltaSync] ⚠️ Merge/Conflict detected! Local v${bill.version} → Server v${response.newVersion}. Applying merged state.`
                    );
                }
                const remoteBill = billDtoToBill(response.mergedBill, 'synced');
                importBillsFromRemote([remoteBill]);
                return;
            }

            if (import.meta.env.DEV) {
                console.log(`[DeltaSync] Success for ${bill.name}. New version: ${response.newVersion}`);
            }

            // 更新版本號 (Remote ID 不變)
            setBillRemoteId(bill.id, bill.remoteId!, bill.shareCode, response.newVersion);

            // 套用 ID 映射 (DeltaIdMappingsDto)
            if (response.idMappings) {
                applyIdMappings(bill.id, {
                    members: cleanIdMap(response.idMappings.members),
                    expenses: cleanIdMap(response.idMappings.expenses),
                    expenseItems: cleanIdMap(response.idMappings.expenseItems),
                });
            }

            markBillAsSynced(bill.id, context?.sentUpdatedAt);
        },
        onError: (error, bill) => {
            const message = error instanceof Error ? error.message : 'Delta Sync failed';
            console.error('[DeltaSync] Error:', message);
            setBillSyncStatus(bill.id, 'error', message);
        }
    });

    const downloadMutation = useMutation({
        mutationFn: downloadBill,
        onSuccess: (dto) => {
            const bill = billDtoToBill(dto, 'synced');
            importBill(bill);
        },
    });

    const syncBill = async (bill: Bill) => {
        // 如果有 remoteId，使用 Delta Sync，否則使用 Full Sync (首次)
        if (bill.remoteId) {
            return deltaMutation.mutateAsync(bill);
        } else {
            return uploadMutation.mutateAsync(bill);
        }
    };

    const fetchBillByShareCode = async (shareCode: string) => {
        return downloadMutation.mutateAsync(shareCode);
    };

    return {
        syncBill,
        fetchBillByShareCode,
        isUploading: uploadMutation.isPending || deltaMutation.isPending,
        isDownloading: downloadMutation.isPending,
        uploadError: uploadMutation.error || deltaMutation.error,
        downloadError: downloadMutation.error,
    };
}

/**
 * 同步帳單 Hook（使用生成的 mutation）
 */
export function useSyncBillMutation() {
    return useBillSync();
}

/**
 * 批次同步多個帳單
 */
export function useBatchSync() {
    const { getUnsyncedBills } = useSnapSplitStore();
    const { syncBill } = useBillSync();

    const syncAllUnsynced = async () => {
        const unsyncedBills = getUnsyncedBills();
        const results: { billId: string; success: boolean; error?: string }[] = [];

        for (const bill of unsyncedBills) {
            try {
                await syncBill(bill);
                results.push({ billId: bill.id, success: true });
            } catch (error) {
                const message = error instanceof Error ? error.message : 'Unknown error';
                results.push({ billId: bill.id, success: false, error: message });
            }
        }

        return results;
    };

    return { syncAllUnsynced };
}