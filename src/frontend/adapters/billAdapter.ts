import type { Bill, SyncStatus } from '@/types/snap-split';
import type { IdMappings } from '@/stores/snapSplitStore';
import type {
    BillDto,
    SyncBillRequestDto,
    SyncBillResponseDto,
    SyncMemberCollectionDto,
    SyncExpenseCollectionDto,
    SyncExpenseItemCollectionDto,
} from '@/api/models';
import { calculateSettlement } from '@/utils/settlement';

// Re-export for convenience
export type { SyncBillRequestDto, SyncBillResponseDto };

/**
 * 後端 BillDto → 前端 Bill
 */
export function billDtoToBill(dto: BillDto, syncStatus: SyncStatus = 'synced'): Bill {
    const members = dto.members ?? [];
    const expenses = dto.expenses ?? [];

    return {
        id: crypto.randomUUID(),
        name: dto.name ?? '',
        members: members.map(m => ({
            id: m.id ?? crypto.randomUUID(),
            name: m.name ?? '',
            remoteId: m.id,
            userId: m.linkedUserId ?? undefined,
            avatarUrl: m.linkedUserAvatarUrl ?? undefined,
            originalName: m.originalName ?? undefined,
            claimedAt: m.claimedAt ?? undefined,
        })),
        expenses: expenses.map(e => ({
            id: e.id ?? crypto.randomUUID(),
            name: e.name ?? '',
            amount: e.amount ?? 0,
            serviceFeePercent: e.serviceFeePercent ?? 0,
            isItemized: e.isItemized ?? false,
            paidById: e.paidById ?? '',
            participants: e.participantIds ?? [],
            items: (e.items ?? []).map(item => ({
                id: item.id ?? crypto.randomUUID(),
                name: item.name ?? '',
                amount: item.amount ?? 0,
                paidById: item.paidById ?? '',
                participants: item.participantIds ?? [],
                remoteId: item.id,
            })),
            remoteId: e.id,
        })),
        settledTransfers: [],
        createdAt: dto.createdAt ?? new Date().toISOString(),
        updatedAt: dto.updatedAt ?? dto.createdAt ?? new Date().toISOString(),
        syncStatus,
        remoteId: dto.id,
        shareCode: dto.shareCode ?? undefined,
        version: dto.version ?? 0,
        lastSyncedAt: new Date().toISOString(),
    };
}

/**
 * 將 settledTransfers 轉換為新格式（加入金額快照）
 * 新格式：`fromId-toId:amount`
 */
function formatSettledTransfersWithAmount(bill: Bill): string[] {
    if (!bill.settledTransfers || bill.settledTransfers.length === 0) {
        return [];
    }

    // 計算當前結算結果以獲取金額
    const settlement = bill.expenses.length > 0 ? calculateSettlement(bill) : null;
    const transferMap = new Map<string, number>();

    // 建立 from-to -> amount 的映射
    if (settlement) {
        for (const t of settlement.transfers) {
            transferMap.set(`${t.from}-${t.to}`, t.amount);
        }
    }

    // 轉換為新格式，優先使用 remoteId
    return bill.settledTransfers.map(st => {
        // 解析現有格式：可能是 "from-to" 或已經是 "from-to:amount"
        const colonIndex = st.lastIndexOf(':');
        let fromTo: string;
        let existingAmount: number | undefined;

        if (colonIndex > 0 && !isNaN(parseFloat(st.substring(colonIndex + 1)))) {
            // 已經是新格式
            fromTo = st.substring(0, colonIndex);
            existingAmount = parseFloat(st.substring(colonIndex + 1));
        } else {
            fromTo = st;
        }

        // 嘗試找到對應的金額（優先使用計算結果，其次使用已存在的金額）
        const amount = transferMap.get(fromTo) ?? existingAmount ?? 0;

        // 嘗試將 localId 轉換為 remoteId
        const [fromId, toId] = fromTo.split('-');
        const fromMember = bill.members.find(m => m.id === fromId);
        const toMember = bill.members.find(m => m.id === toId);

        // 優先使用 remoteId，如果沒有則使用 localId
        const finalFromId = fromMember?.remoteId || fromId;
        const finalToId = toMember?.remoteId || toId;

        return `${finalFromId}-${finalToId}:${amount.toFixed(2)}`;
    });
}

/**
 * 前端 Bill → 同步請求 DTO
 */
export function billToSyncRequest(bill: Bill): SyncBillRequestDto {
    const members: SyncMemberCollectionDto = {
        // 重要：同步時一律送出所有成員，確保後端能建立完整的 LocalId -> RemoteId 映射表
        // 這對於正確關聯費用 (PaidBy, Participants) 至關重要
        upsert: bill.members.map((m, index) => ({
            localId: m.id,
            remoteId: m.remoteId || undefined,
            name: m.name,
            displayOrder: index,
            linkedUserId: m.userId || undefined,
            claimedAt: m.claimedAt || undefined,
        })),
        deletedIds: (bill.deletedMemberIds || []).filter(id => !!id),
    };

    const expenses: SyncExpenseCollectionDto = {
        upsert: bill.expenses.map(e => {
            const items: SyncExpenseItemCollectionDto | undefined = e.isItemized ? {
                upsert: e.items.map(item => ({
                    localId: item.id,
                    remoteId: item.remoteId || undefined,
                    name: item.name,
                    amount: item.amount,
                    paidByLocalId: item.paidById,
                    participantLocalIds: item.participants,
                })),
                // 使用追蹤的 deletedItemIds（已優先使用 remoteId）
                deletedIds: (e.deletedItemIds || []).filter(id => !!id),
            } : undefined;

            return {
                localId: e.id,
                remoteId: e.remoteId || undefined,
                name: e.name,
                amount: e.amount,
                serviceFeePercent: e.serviceFeePercent,
                isItemized: e.isItemized,
                // 確保傳送的是 Member 的 LocalId (id)，而非 userId
                paidByLocalId: e.paidById || undefined,
                participantLocalIds: e.participants,
                items: items,
            };
        }),
        deletedIds: (bill.deletedExpenseIds || []).filter(id => !!id),
    };

    const request: SyncBillRequestDto = {
        remoteId: bill.remoteId || undefined,
        localId: bill.id,
        baseVersion: bill.version || 0,
        name: bill.name,
        members,
        expenses,
        // 使用新格式：加入金額快照
        settledTransfers: formatSettledTransfersWithAmount(bill),
        localUpdatedAt: bill.updatedAt,
    };

    if (import.meta.env.DEV) {
        console.log('[Adapter] Generated Sync Request:', JSON.stringify(request, null, 2));
    }

    return request;
}

/**
 * 將同步回應的 ID 映射轉換為 store 可用格式
 */
export function responseToIdMappings(response: SyncBillResponseDto): IdMappings {
    const idMappings = response.idMappings ?? { members: {}, expenses: {}, expenseItems: {} };
    return {
        members: (idMappings.members ?? {}) as Record<string, string>,
        expenses: (idMappings.expenses ?? {}) as Record<string, string>,
        expenseItems: (idMappings.expenseItems ?? {}) as Record<string, string>,
    };
}

/**
 * 合併遠端帳單到本地（用於衝突解決）
 */
export function mergeRemoteBill(local: Bill, remote: BillDto): Bill {
    return {
        ...billDtoToBill(remote, 'synced'),
        id: local.id,
        settledTransfers: local.settledTransfers,
    };
}
