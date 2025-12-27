import type { Bill, Expense, MemberSummary, SettlementResult, Transfer } from "@/types/snap-split";

/**
 * 計算含服務費的金額
 */
export function applyServiceFee(amount: number, serviceFeePercent: number): number {
    return amount * (1 + serviceFeePercent / 100);
}

/**
 * 計算單筆消費的總金額（含服務費）
 */
export function getExpenseTotal(expense: Expense): number {
    if (expense.isItemized) {
        const itemsTotal = expense.items.reduce((sum, item) => sum + item.amount, 0);
        return applyServiceFee(itemsTotal, expense.serviceFeePercent);
    }
    return applyServiceFee(expense.amount, expense.serviceFeePercent);
}

/**
 * 計算單筆消費的原始金額（不含服務費）
 */
export function getExpenseAmount(expense: Expense): number {
    if (expense.isItemized) {
        return expense.items.reduce((sum, item) => sum + item.amount, 0);
    }
    return expense.amount;
}

/**
 * 處理簡單模式消費
 */
function processSimpleExpense(
    expense: Expense,
    summaryMap: Map<string, MemberSummary>
): void {
    const amountWithFee = applyServiceFee(expense.amount, expense.serviceFeePercent);
    const participantCount = expense.participants.length;

    if (participantCount === 0) return;

    const sharePerPerson = amountWithFee / participantCount;

    // 付款人增加實付金額
    const payer = summaryMap.get(expense.paidBy);
    if (payer) {
        payer.totalPaid += amountWithFee;
    }

    // 參與者增加應付金額
    for (const participantId of expense.participants) {
        const participant = summaryMap.get(participantId);
        if (participant) {
            participant.totalOwed += sharePerPerson;
        }
    }
}

/**
 * 處理品項模式消費
 */
function processItemizedExpense(
    expense: Expense,
    summaryMap: Map<string, MemberSummary>
): void {
    if (expense.items.length === 0) return;

    const serviceFeeMultiplier = 1 + expense.serviceFeePercent / 100;

    for (const item of expense.items) {
        const itemWithFee = item.amount * serviceFeeMultiplier;
        const participantCount = item.participants.length;

        if (participantCount === 0) continue;

        const sharePerPerson = itemWithFee / participantCount;

        // 付款人增加實付
        const payer = summaryMap.get(item.paidBy);
        if (payer) {
            payer.totalPaid += itemWithFee;
        }

        // 參與者增加應付
        for (const pid of item.participants) {
            const participant = summaryMap.get(pid);
            if (participant) {
                participant.totalOwed += sharePerPerson;
            }
        }
    }
}

/**
 * 計算每位成員的摘要（應付、實付、餘額）
 */
function calculateMemberSummaries(bill: Bill): MemberSummary[] {
    const { members, expenses } = bill;

    // 初始化每位成員的摘要
    const summaryMap = new Map<string, MemberSummary>(
        members.map(m => [m.id, {
            memberId: m.id,
            totalPaid: 0,
            totalOwed: 0,
            balance: 0,
        }])
    );

    // 處理每筆消費
    for (const expense of expenses) {
        if (expense.isItemized) {
            processItemizedExpense(expense, summaryMap);
        } else {
            processSimpleExpense(expense, summaryMap);
        }
    }

    // 計算餘額 (實付 - 應付) 並四捨五入
    for (const summary of summaryMap.values()) {
        summary.balance = summary.totalPaid - summary.totalOwed;
        summary.totalPaid = Math.round(summary.totalPaid * 100) / 100;
        summary.totalOwed = Math.round(summary.totalOwed * 100) / 100;
        summary.balance = Math.round(summary.balance * 100) / 100;
    }

    return Array.from(summaryMap.values());
}

/**
 * 使用貪婪法產生最小化轉帳清單
 */
function calculateTransfers(memberSummaries: MemberSummary[]): Transfer[] {
    const creditors: { id: string; amount: number }[] = [];
    const debtors: { id: string; amount: number }[] = [];

    for (const summary of memberSummaries) {
        if (summary.balance > 0.01) {
            creditors.push({ id: summary.memberId, amount: summary.balance });
        } else if (summary.balance < -0.01) {
            debtors.push({ id: summary.memberId, amount: Math.abs(summary.balance) });
        }
    }

    creditors.sort((a, b) => b.amount - a.amount);
    debtors.sort((a, b) => b.amount - a.amount);

    const transfers: Transfer[] = [];

    while (creditors.length > 0 && debtors.length > 0) {
        const creditor = creditors[0];
        const debtor = debtors[0];
        const transferAmount = Math.min(creditor.amount, debtor.amount);

        if (transferAmount > 0.01) {
            transfers.push({
                from: debtor.id,
                to: creditor.id,
                amount: Math.round(transferAmount * 100) / 100,
            });
        }

        creditor.amount -= transferAmount;
        debtor.amount -= transferAmount;

        if (creditor.amount < 0.01) creditors.shift();
        if (debtor.amount < 0.01) debtors.shift();
    }

    return transfers;
}

/**
 * 計算帳單的完整結算結果
 */
export function calculateSettlement(bill: Bill): SettlementResult {
    const { expenses } = bill;

    const totalAmount = expenses.reduce((sum, e) => sum + getExpenseAmount(e), 0);
    const totalWithServiceFee = expenses.reduce((sum, e) => sum + getExpenseTotal(e), 0);

    const memberSummaries = calculateMemberSummaries(bill);
    const transfers = calculateTransfers(memberSummaries);

    return {
        totalAmount: Math.round(totalAmount * 100) / 100,
        totalWithServiceFee: Math.round(totalWithServiceFee * 100) / 100,
        memberSummaries,
        transfers,
    };
}

/**
 * 檢查轉帳是否已結清
 */
export function isTransferSettled(
    settledTransfers: string[] | undefined,
    fromId: string,
    toId: string
): boolean {
    return settledTransfers?.includes(`${fromId}-${toId}`) ?? false;
}

/**
 * 格式化金額顯示
 */
export function formatAmount(amount: number): string {
    return `$${amount.toLocaleString('zh-TW', { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;
}

/**
 * 根據成員 ID 取得成員名稱
 */
export function getMemberName(members: { id: string; name: string }[], id: string): string {
    return members.find(m => m.id === id)?.name ?? '未知';
}

// 柔和/消光色系調色盤 (Morandi / Muted Pastels)
export const MATTE_PALETTE = [
    '#7E9aa8', // Muted Blue
    '#9AA87E', // Muted Green
    '#A87E7E', // Muted Red
    '#A8967E', // Muted Brown/Gold
    '#8F7EA8', // Muted Purple
    '#7EA89E', // Muted Teal
    '#E0B0A8', // Soft Pink
    '#9DB4C0', // Blue Grey
    '#C2B280', // Sand
    '#E6B8A2', // Peach
    '#778DA9', // Air Force Blue
    '#6B705C', // Olive Drab
    '#CB997E', // Terracotta
    '#A5A58D', // Sage
    '#B7B7A4', // Ash
];

/**
 * 根據成員在列表中的順序取得對應的顏色
 * Get color based on member index to ensure uniqueness in small groups
 */
export function getMemberColor(memberId: string, members: { id: string }[]): string {
    const index = members.findIndex(m => m.id === memberId);
    if (index === -1) return MATTE_PALETTE[0]; // Fallback
    return MATTE_PALETTE[index % MATTE_PALETTE.length];
}
