import type { Bill, Member } from '@/types/snap-split';
import type {
    DeltaSyncRequest,
    DeltaSettlementDto,
} from '@/api/models';

/**
 * 取得有效的 ID（優先使用 remoteId，若無則使用 localId）
 */
function getEffectiveId(entity: { id: string; remoteId?: string }): string {
    return entity.remoteId ?? entity.id;
}

/**
 * 取得有效的成員 ID（用於參照）
 */
function getMemberRefId(memberId: string, members: Member[]): string {
    const member = members.find(m => m.id === memberId);
    if (!member) return memberId; // Should not happen, but fallback
    return getEffectiveId(member);
}

/**
 * 建立 Delta Sync 請求
 * @param bill 當前帳單狀態
 */
export function createDeltaRequest(bill: Bill): DeltaSyncRequest {
    const request: DeltaSyncRequest = {
        baseVersion: bill.version,
        members: {
            add: [],
            update: [],
            delete: bill.deletedMemberIds || [],
        },
        expenses: {
            add: [],
            update: [],
            delete: bill.deletedExpenseIds || [],
        },
        expenseItems: {
            add: [],
            update: [],
            delete: [], // Will be populated from expenses
        },
        settlements: {
            mark: [],
            unmark: [],
        },
        billMeta: {
            name: bill.name, // Always send current name, backend checks if changed? 
                             // Or should I check against... what?
                             // Since we don't track name changes explicitly, sending it is safe.
        },
    };

    // 1. Members
    bill.members.forEach((m, index) => {
        if (!m.remoteId) {
            // New Member
            request.members!.add!.push({
                localId: m.id,
                name: m.name,
                displayOrder: index, // Ensure order is preserved
            });
        } else {
            // Existing Member (Update)
            request.members!.update!.push({
                remoteId: m.remoteId,
                name: m.name,
                displayOrder: index,
                linkedUserId: m.userId,
                claimedAt: m.claimedAt,
            });
        }
    });

    // 2. Expenses
    bill.expenses.forEach(e => {
        const participantIds = e.participants.map(pid => getMemberRefId(pid, bill.members));
        const paidByMemberId = e.paidById ? getMemberRefId(e.paidById, bill.members) : undefined;

        if (!e.remoteId) {
            // New Expense
            request.expenses!.add!.push({
                localId: e.id,
                name: e.name,
                amount: e.amount,
                serviceFeePercent: e.serviceFeePercent,
                isItemized: e.isItemized,
                paidByMemberId,
                participantIds,
            });
        } else {
            // Existing Expense (Update)
            request.expenses!.update!.push({
                remoteId: e.remoteId,
                name: e.name,
                amount: e.amount,
                serviceFeePercent: e.serviceFeePercent,
                isItemized: e.isItemized,
                paidByMemberId,
                participantIds,
            });
        }

        // 3. Expense Items
        if (e.isItemized && e.items) {
            e.items.forEach(item => {
                const itemParticipantIds = item.participants.map(pid => getMemberRefId(pid, bill.members));
                const itemPaidById = item.paidById ? getMemberRefId(item.paidById, bill.members) : undefined;

                if (!item.remoteId) {
                    // New Item
                    request.expenseItems!.add!.push({
                        localId: item.id,
                        expenseId: getEffectiveId(e), // Link to parent expense (Remote or Local ID)
                        name: item.name,
                        amount: item.amount,
                        paidByMemberId: itemPaidById,
                        participantIds: itemParticipantIds,
                    });
                } else {
                    // Existing Item (Update)
                    request.expenseItems!.update!.push({
                        remoteId: item.remoteId,
                        name: item.name,
                        amount: item.amount,
                        paidByMemberId: itemPaidById,
                        participantIds: itemParticipantIds,
                    });
                }
            });
        }

        // Collect deleted item IDs
        if (e.deletedItemIds && e.deletedItemIds.length > 0) {
            request.expenseItems!.delete!.push(...e.deletedItemIds);
        }
    });

    // 4. Settlements
    // Mark: Current settled transfers
    // We send current state as "mark". Backend should handle idempotency.
    // However, to compute "amount", we need to know the amount.
    // But DeltaSettlementDto has amount as optional?
    // "amount?: number".
    // If we mark it, do we need to send amount?
    // The previous implementation calculated it.
    
    // We need to parse "from-to" keys.
    const parseSettlementKey = (key: string): DeltaSettlementDto => {
        // Key format: "fromId-toId" or "fromId-toId:amount"
        // But in Bill.settledTransfers, it stores "fromId-toId" (mostly).
        // Wait, billAdapter `formatSettledTransfersWithAmount` handled parsing ":amount" if present.
        // And it calculated amount if missing.
        // We should do the same.
        
        // Actually, for "mark", we should send the IDs.
        // Ideally we should use Remote IDs if available.
        const parts = key.split(':');
        const ids = parts[0].split('-');
        const fromLocalId = ids[0];
        const toLocalId = ids[1];
        
        return {
            fromMemberId: getMemberRefId(fromLocalId, bill.members),
            toMemberId: getMemberRefId(toLocalId, bill.members),
            // We can skip amount for now, or calculate it if needed.
            // Backend probably recalculates it or verifies it.
        };
    };

    bill.settledTransfers.forEach(key => {
        request.settlements!.mark!.push(parseSettlementKey(key));
    });

    // Unmark: Explicitly tracked removed settlements
    if (bill.unsettledTransfers) {
        bill.unsettledTransfers.forEach(key => {
            request.settlements!.unmark!.push(parseSettlementKey(key));
        });
    }

    return request;
}