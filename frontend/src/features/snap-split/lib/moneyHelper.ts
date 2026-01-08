/**
 * 金額計算工具
 * @see SNAPSPLIT_SPEC.md Section 8.4
 * @description 前端實作與後端 Kayden.Commons.Extensions.MoneyHelper 保持一致
 */

/**
 * 將金額平均分配給指定人數，使用 Penny Allocation（最大餘數法）確保總和精確
 * @param total 總金額
 * @param count 人數
 * @returns 每人分攤金額陣列（前面的人可能多分攤 0.01）
 * @throws 人數必須大於 0
 * @example
 * allocate(100, 3) => [33.34, 33.33, 33.33]
 * allocate(100, 4) => [25.00, 25.00, 25.00, 25.00]
 * allocate(10, 3) => [3.34, 3.33, 3.33]
 */
export function allocate(total: number, count: number): number[] {
    if (count <= 0) {
        throw new Error('人數必須大於 0');
    }

    if (count === 1) {
        return [total];
    }

    // 1. 計算基礎金額（向下取整到分）
    const baseAmount = Math.floor((total / count) * 100) / 100;

    // 2. 計算剩餘金額（以分為單位）
    const totalCents = Math.round(total * 100);
    const baseCents = Math.round(baseAmount * 100);
    const remainderCents = totalCents - baseCents * count;

    // 3. 分配：前 remainder 人多得 1 分
    const result: number[] = [];
    for (let i = 0; i < count; i++) {
        // 使用 roundToTwoDecimals 避免浮點數精度問題
        const amount = i < remainderCents ? baseAmount + 0.01 : baseAmount;
        result.push(roundToTwoDecimals(amount));
    }

    return result;
}

/**
 * 計算含服務費的總金額
 * @param amount 原始金額
 * @param serviceFeePercent 服務費百分比（例如 10 表示 10%）
 * @returns 含服務費的總金額（四捨五入到分）
 */
export function calculateAmountWithServiceFee(
    amount: number,
    serviceFeePercent: number
): number {
    const total = amount * (1 + serviceFeePercent / 100);
    return roundToTwoDecimals(total);
}

/**
 * 計算服務費金額
 * @param amount 原始金額
 * @param serviceFeePercent 服務費百分比（例如 10 表示 10%）
 * @returns 服務費金額（四捨五入到分）
 */
export function calculateServiceFee(
    amount: number,
    serviceFeePercent: number
): number {
    const fee = (amount * serviceFeePercent) / 100;
    return roundToTwoDecimals(fee);
}

/**
 * 四捨五入到小數點後兩位（銀行家捨入法）
 * @param value 數值
 * @returns 四捨五入後的數值
 */
function roundToTwoDecimals(value: number): number {
    return Math.round((value + Number.EPSILON) * 100) / 100;
}
