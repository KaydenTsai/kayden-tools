namespace Kayden.Commons.Extensions;

/// <summary>
/// 金額計算工具類別
/// </summary>
public static class MoneyHelper
{
    /// <summary>
    /// 將金額平均分配給指定人數，使用 Penny Allocation（最大餘數法）確保總和精確
    /// </summary>
    /// <param name="total">總金額</param>
    /// <param name="count">人數</param>
    /// <returns>每人分攤金額陣列（前面的人可能多分攤 0.01）</returns>
    /// <exception cref="ArgumentException">人數必須大於 0</exception>
    /// <example>
    /// Allocate(100m, 3) => [33.34m, 33.33m, 33.33m]
    /// Allocate(100m, 4) => [25.00m, 25.00m, 25.00m, 25.00m]
    /// Allocate(10m, 3) => [3.34m, 3.33m, 3.33m]
    /// </example>
    public static decimal[] Allocate(decimal total, int count)
    {
        if (count <= 0)
            throw new ArgumentException("人數必須大於 0", nameof(count));

        if (count == 1)
            return [total];

        // 1. 計算基礎金額（向下取整到分）
        var baseAmount = Math.Floor(total / count * 100) / 100;

        // 2. 計算剩餘金額（以分為單位）
        var totalCents = (int)(total * 100);
        var baseCents = (int)(baseAmount * 100);
        var remainderCents = totalCents - baseCents * count;

        // 3. 分配：前 remainder 人多得 1 分
        var result = new decimal[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = baseAmount + (i < remainderCents ? 0.01m : 0m);
        }

        return result;
    }

    /// <summary>
    /// 計算含服務費的總金額
    /// </summary>
    /// <param name="amount">原始金額</param>
    /// <param name="serviceFeePercent">服務費百分比（例如 10 表示 10%）</param>
    /// <returns>含服務費的總金額（四捨五入到分）</returns>
    public static decimal CalculateAmountWithServiceFee(decimal amount, decimal serviceFeePercent)
    {
        var total = amount * (1 + serviceFeePercent / 100);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// 計算服務費金額
    /// </summary>
    /// <param name="amount">原始金額</param>
    /// <param name="serviceFeePercent">服務費百分比（例如 10 表示 10%）</param>
    /// <returns>服務費金額（四捨五入到分）</returns>
    public static decimal CalculateServiceFee(decimal amount, decimal serviceFeePercent)
    {
        var fee = amount * serviceFeePercent / 100;
        return Math.Round(fee, 2, MidpointRounding.AwayFromZero);
    }
}
