export interface Member {
    id: string;
    name: string;
}

export interface ExpenseItem {
    id: string;
    name: string;
    amount: number;
    paidBy: string;
    participants: string[];
}

export interface Expense {
    id: string;
    name: string;
    serviceFeePercent: number;

    // 模式：簡單模式 vs 品項模式
    isItemized: boolean;

    // 簡單模式欄位
    amount: number;
    paidBy: string;
    participants: string[];

    // 品項模式欄位
    items: ExpenseItem[];
}

export interface Bill {
    id: string;
    name: string;
    members: Member[];
    expenses: Expense[];
    settledTransfers: string[];
    createdAt: string;
    updatedAt: string;
}

export interface Transfer {
    from: string;
    to: string;
    amount: number;
}

export interface MemberSummary {
    memberId: string;
    totalPaid: number;
    totalOwed: number;
    balance: number;
}

export interface SettlementResult {
    totalAmount: number;
    totalWithServiceFee: number;
    memberSummaries: MemberSummary[];
    transfers: Transfer[];
}
