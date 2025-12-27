import type { Bill, SyncStatus } from '@/types/snap-split';
import type { IdMappings } from '@/stores/snapSplitStore';
import type { BillDto } from '@/api/models';

/** 同步請求 DTO */
export interface SyncBillRequestDto {
    remoteId?: string;
    localId: string;
    name: string;
    members: SyncMemberDto[];
    expenses: SyncExpenseDto[];
    settledTransfers: string[];
    localUpdatedAt: string;
}

/** 同步成員 DTO */
export interface SyncMemberDto {
    localId: string;
    remoteId?: string;
    name: string;
    displayOrder: number;
}

/** 同步費用 DTO */
export interface SyncExpenseDto {
    localId: string;
    remoteId?: string;
    name: string;
    amount: number;
    serviceFeePercent: number;
    isItemized: boolean;
    paidByLocalId?: string;
    participantLocalIds: string[];
    items?: SyncExpenseItemDto[];
}

/** 同步費用細項 DTO */
export interface SyncExpenseItemDto {
    localId: string;
    remoteId?: string;
    name: string;
    amount: number;
    paidByLocalId: string;
    participantLocalIds: string[];
}

/** 同步回應 DTO */
export interface SyncBillResponseDto {
    remoteId: string;
    shareCode?: string;
    idMappings: {
        members: Record<string, string>;
        expenses: Record<string, string>;
        expenseItems: Record<string, string>;
    };
    serverTimestamp: string;
}

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
        })),
        expenses: expenses.map(e => ({
            id: e.id ?? crypto.randomUUID(),
            name: e.name ?? '',
            amount: e.amount ?? 0,
            serviceFeePercent: e.serviceFeePercent ?? 0,
            isItemized: e.isItemized ?? false,
            paidBy: e.paidById ?? '',
            participants: e.participantIds ?? [],
            items: (e.items ?? []).map(item => ({
                id: item.id ?? crypto.randomUUID(),
                name: item.name ?? '',
                amount: item.amount ?? 0,
                paidBy: item.paidById ?? '',
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
        lastSyncedAt: new Date().toISOString(),
    };
}

/**
 * 前端 Bill → 同步請求 DTO
 */
export function billToSyncRequest(bill: Bill): SyncBillRequestDto {
    return {
        remoteId: bill.remoteId,
        localId: bill.id,
        name: bill.name,
        members: bill.members.map((m, index) => ({
            localId: m.id,
            remoteId: m.remoteId,
            name: m.name,
            displayOrder: index,
        })),
        expenses: bill.expenses.map(e => ({
            localId: e.id,
            remoteId: e.remoteId,
            name: e.name,
            amount: e.amount,
            serviceFeePercent: e.serviceFeePercent,
            isItemized: e.isItemized,
            paidByLocalId: e.paidBy || undefined,
            participantLocalIds: e.participants,
            items: e.isItemized ? e.items.map(item => ({
                localId: item.id,
                remoteId: item.remoteId,
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
 * 將同步回應的 ID 映射轉換為 store 可用格式
 */
export function responseToIdMappings(response: SyncBillResponseDto): IdMappings {
    return {
        members: response.idMappings.members,
        expenses: response.idMappings.expenses,
        expenseItems: response.idMappings.expenseItems,
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
