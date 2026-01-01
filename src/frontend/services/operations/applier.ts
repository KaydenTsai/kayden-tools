import type { Bill } from "@/types/snap-split";
import type { Operation } from "../signalr/billConnection";

/**
 * 將操作套用到帳單狀態
 * @param bill 現有的帳單狀態
 * @param op 要套用的操作
 * @returns 更新後的帳單狀態
 */
export function applyOperationToBill(bill: Bill, op: Operation): Bill {
    const { opType, targetId, payload, version } = op;
    const newBill = { ...bill, version: Math.max(bill.version, version) };

    switch (opType) {
        case "BILL_UPDATE_META":
            if (payload.name) newBill.name = payload.name;
            break;

        case "MEMBER_ADD":
            if (targetId) {
                newBill.members = [...bill.members, {
                    id: targetId,
                    name: payload.name || "New Member",
                }];
            }
            break;

        case "MEMBER_UPDATE":
            newBill.members = bill.members.map(m =>
                m.id === targetId ? { ...m, ...payload } : m
            );
            break;

        case "MEMBER_CLAIM":
            newBill.members = bill.members.map(m => {
                if (m.id !== targetId) return m;
                return {
                    ...m,
                    originalName: m.name,
                    userId: payload.userId,
                    avatarUrl: payload.avatarUrl,
                    claimedAt: new Date().toISOString(),
                };
            });
            break;

        case "MEMBER_UNCLAIM":
            newBill.members = bill.members.map(m => {
                if (m.id !== targetId) return m;
                return {
                    ...m,
                    name: m.originalName ?? m.name,
                    originalName: undefined,
                    userId: undefined,
                    avatarUrl: undefined,
                    claimedAt: undefined,
                };
            });
            break;

        case "MEMBER_REORDER":
            if (Array.isArray(payload.order)) {
                const orderMap = new Map<string, number>(
                    payload.order.map((id: string, idx: number) => [id, idx])
                );
                newBill.members = [...bill.members].sort((a, b) => {
                    const aIdx = orderMap.get(a.id) ?? Number.MAX_VALUE;
                    const bIdx = orderMap.get(b.id) ?? Number.MAX_VALUE;
                    return aIdx - bIdx;
                });
            }
            break;

        case "MEMBER_REMOVE": {
            // 追蹤被刪除的成員 ID（優先使用 remoteId）
            const removedMember = bill.members.find(m => m.id === targetId);
            if (removedMember) {
                const deletedId = removedMember.remoteId || removedMember.id;
                newBill.deletedMemberIds = [...(bill.deletedMemberIds || []), deletedId];
            }
            newBill.members = bill.members.filter(m => m.id !== targetId);
            // 同時清理費用中的引用
            newBill.expenses = bill.expenses.map(e => ({
                ...e,
                paidById: e.paidById === targetId ? "" : e.paidById,
                participants: e.participants.filter(p => p !== targetId),
            }));
            break;
        }

        case "EXPENSE_ADD":
            if (targetId) {
                newBill.expenses = [...bill.expenses, {
                    id: targetId,
                    name: payload.name || "New Expense",
                    amount: payload.amount || 0,
                    serviceFeePercent: payload.serviceFeePercent || 0,
                    isItemized: payload.isItemized || false,
                    paidById: payload.paidById || "",
                    participants: [],
                    items: [],
                }];
            }
            break;

        case "EXPENSE_UPDATE":
            newBill.expenses = bill.expenses.map(e => 
                e.id === targetId ? { ...e, ...payload, paidById: payload.paidById ?? e.paidById } : e
            );
            break;

        case "EXPENSE_DELETE": {
            // 追蹤被刪除的費用 ID（優先使用 remoteId）
            const removedExpense = bill.expenses.find(e => e.id === targetId);
            if (removedExpense) {
                const deletedId = removedExpense.remoteId || removedExpense.id;
                newBill.deletedExpenseIds = [...(bill.deletedExpenseIds || []), deletedId];
            }
            newBill.expenses = bill.expenses.filter(e => e.id !== targetId);
            break;
        }
            
        case "EXPENSE_SET_PARTICIPANTS":
            newBill.expenses = bill.expenses.map(e =>
                e.id === targetId ? { ...e, participants: payload.participantIds ?? payload.memberIds ?? [] } : e
            );
            break;

        case "EXPENSE_TOGGLE_ITEMIZED":
            newBill.expenses = bill.expenses.map(e =>
                e.id === targetId ? { ...e, isItemized: !e.isItemized } : e
            );
            break;

        // Item 操作
        case "ITEM_ADD":
            newBill.expenses = bill.expenses.map(e => {
                if (e.id !== payload.expenseId) return e;
                return {
                    ...e,
                    items: [...e.items, {
                        id: targetId ?? crypto.randomUUID(),
                        name: payload.name || "New Item",
                        amount: payload.amount || 0,
                        paidById: payload.paidById || e.paidById,
                        participants: [],
                    }],
                };
            });
            break;

        case "ITEM_UPDATE":
            newBill.expenses = bill.expenses.map(e => ({
                ...e,
                items: e.items.map(item =>
                    item.id === targetId
                        ? { ...item, ...payload, paidById: payload.paidById ?? item.paidById }
                        : item
                ),
            }));
            break;

        case "ITEM_DELETE":
            newBill.expenses = bill.expenses.map(e => {
                const removedItem = e.items.find(item => item.id === targetId);
                if (removedItem) {
                    // 追蹤被刪除的 Item ID（優先使用 remoteId）
                    const deletedId = removedItem.remoteId || removedItem.id;
                    return {
                        ...e,
                        items: e.items.filter(item => item.id !== targetId),
                        deletedItemIds: [...(e.deletedItemIds || []), deletedId],
                    };
                }
                return e;
            });
            break;

        case "ITEM_SET_PARTICIPANTS":
            newBill.expenses = bill.expenses.map(e => ({
                ...e,
                items: e.items.map(item =>
                    item.id === targetId
                        ? { ...item, participants: payload.participantIds ?? [] }
                        : item
                ),
            }));
            break;

        // Settlement 操作
        case "SETTLEMENT_TOGGLE": {
            const key = `${payload.fromId}-${payload.toId}`;
            const isSettled = bill.settledTransfers.includes(key);
            newBill.settledTransfers = isSettled
                ? bill.settledTransfers.filter(t => t !== key)
                : [...bill.settledTransfers, key];
            
            if (isSettled) {
                newBill.unsettledTransfers = [...(bill.unsettledTransfers || []), key];
            } else {
                newBill.unsettledTransfers = (bill.unsettledTransfers || []).filter(t => t !== key);
            }
            break;
        }

        case "SETTLEMENT_MARK": {
            const markKey = `${payload.fromMemberId}-${payload.toMemberId}`;
            if (!bill.settledTransfers.includes(markKey)) {
                newBill.settledTransfers = [...bill.settledTransfers, markKey];
                newBill.unsettledTransfers = (bill.unsettledTransfers || []).filter(t => t !== markKey);
            }
            break;
        }

        case "SETTLEMENT_UNMARK": {
            const unmarkKey = `${payload.fromMemberId}-${payload.toMemberId}`;
            if (bill.settledTransfers.includes(unmarkKey)) {
                newBill.unsettledTransfers = [...(bill.unsettledTransfers || []), unmarkKey];
            }
            newBill.settledTransfers = bill.settledTransfers.filter(t => t !== unmarkKey);
            break;
        }

        case "SETTLEMENT_CLEAR_ALL":
            newBill.unsettledTransfers = [...(bill.unsettledTransfers || []), ...bill.settledTransfers];
            newBill.settledTransfers = [];
            break;

        default:
            console.warn(`[Applier] Unknown operation type: ${opType}`);
    }

    return newBill;
}
