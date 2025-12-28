import { useMutation } from '@tanstack/react-query';
import {
    getApiBillsShareShareCode,
    postApiBillsSync,
    usePostApiBillsSync,
} from '@/api';
import type {
    BillDto,
    BillDtoApiResponse,
    SyncBillRequestDto,
    SyncBillResponseDto,
    SyncBillResponseDtoApiResponse,
} from '@/api';
import { useSnapSplitStore } from '@/stores/snapSplitStore';
import { billDtoToBill } from '@/adapters/billAdapter';
import type { Bill } from '@/types/snap-split';

/**
 * 將前端 Bill 轉換為 SyncBillRequestDto
 */
function billToSyncRequest(bill: Bill): SyncBillRequestDto {
    return {
        remoteId: bill.remoteId ?? undefined,
        localId: bill.id,
        name: bill.name,
        members: bill.members.map((m, index) => ({
            localId: m.id,
            remoteId: m.remoteId ?? undefined,
            name: m.name,
            displayOrder: index,
            linkedUserId: m.userId,
            claimedAt: m.claimedAt,
        })),
        expenses: bill.expenses.map(e => ({
            localId: e.id,
            remoteId: e.remoteId ?? undefined,
            name: e.name,
            amount: e.amount,
            serviceFeePercent: e.serviceFeePercent,
            isItemized: e.isItemized,
            paidByLocalId: e.paidBy || undefined,
            participantLocalIds: e.participants,
            items: e.isItemized ? e.items.map(item => ({
                localId: item.id,
                remoteId: item.remoteId ?? undefined,
                name: item.name,
                amount: item.amount,
                paidByLocalId: item.paidBy,
                participantLocalIds: item.participants,
            })) : undefined,
        })),
        settledTransfers: bill.settledTransfers,
        localUpdatedAt: bill.updatedAt,
    };
}

/**
 * 從伺服器下載帳單（使用生成的 API）
 */
async function downloadBill(shareCode: string): Promise<BillDto> {
    const response = await getApiBillsShareShareCode(shareCode);
    const apiResponse = response as BillDtoApiResponse;

    if (!apiResponse.success || !apiResponse.data) {
        throw new Error(apiResponse.error?.message ?? 'Download failed');
    }

    return apiResponse.data;
}

/**
 * 上傳帳單到伺服器（使用生成的 sync API）
 */
async function uploadBill(bill: Bill): Promise<SyncBillResponseDto> {
    const request = billToSyncRequest(bill);
    const response = await postApiBillsSync(request);
    const apiResponse = response as SyncBillResponseDtoApiResponse;

    if (!apiResponse.success || !apiResponse.data) {
        throw new Error(apiResponse.error?.message ?? 'Sync failed');
    }

    return apiResponse.data;
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
    } = useSnapSplitStore();

    const uploadMutation = useMutation({
        mutationFn: uploadBill,
        onMutate: (bill) => {
            setBillSyncStatus(bill.id, 'syncing');
        },
        onSuccess: (response, bill) => {
            setBillRemoteId(bill.id, response.remoteId!, response.shareCode ?? undefined);

            // 套用 ID 映射
            if (response.idMappings) {
                applyIdMappings(bill.id, {
                    members: response.idMappings.members ?? {},
                    expenses: response.idMappings.expenses ?? {},
                    expenseItems: response.idMappings.expenseItems ?? {},
                });
            }

            markBillAsSynced(bill.id);
        },
        onError: (error, bill) => {
            const message = error instanceof Error ? error.message : 'Sync failed';
            setBillSyncStatus(bill.id, 'error', message);
        },
    });

    const downloadMutation = useMutation({
        mutationFn: downloadBill,
        onSuccess: (dto) => {
            const bill = billDtoToBill(dto, 'synced');
            importBill(bill);
        },
    });

    const syncBill = async (bill: Bill) => {
        return uploadMutation.mutateAsync(bill);
    };

    const fetchBillByShareCode = async (shareCode: string) => {
        return downloadMutation.mutateAsync(shareCode);
    };

    return {
        syncBill,
        fetchBillByShareCode,
        isUploading: uploadMutation.isPending,
        isDownloading: downloadMutation.isPending,
        uploadError: uploadMutation.error,
        downloadError: downloadMutation.error,
    };
}

/**
 * 同步帳單 Hook（使用生成的 mutation）
 */
export function useSyncBillMutation() {
    return usePostApiBillsSync();
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
