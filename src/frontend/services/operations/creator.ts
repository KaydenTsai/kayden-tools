import type { OperationRequest } from "../signalr/billConnection";

/**
 * 建立操作請求
 */
export function createOperationRequest(
    billId: string,
    baseVersion: number,
    opType: string,
    targetId: string | undefined,
    payload: any
): OperationRequest {
    return {
        clientId: crypto.randomUUID(),
        billId,
        opType,
        targetId,
        payload,
        baseVersion
    };
}

export const ops = {
    updateBillName: (name: string) => ({ type: "BILL_UPDATE_META", payload: { name } }),
    addMember: (name: string) => ({ type: "MEMBER_ADD", targetId: crypto.randomUUID(), payload: { name } }),
    removeMember: (id: string) => ({ type: "MEMBER_REMOVE", targetId: id, payload: {} }),
    addExpense: (name: string, amount: number, paidById: string) => ({ 
        type: "EXPENSE_ADD", 
        targetId: crypto.randomUUID(), 
        payload: { name, amount, paidById } 
    }),
    updateExpense: (id: string, updates: any) => ({ type: "EXPENSE_UPDATE", targetId: id, payload: updates }),
    deleteExpense: (id: string) => ({ type: "EXPENSE_DELETE", targetId: id, payload: {} }),
    setParticipants: (expenseId: string, memberIds: string[]) => ({ 
        type: "EXPENSE_SET_PARTICIPANTS", 
        targetId: expenseId, 
        payload: { memberIds } 
    }),
};
