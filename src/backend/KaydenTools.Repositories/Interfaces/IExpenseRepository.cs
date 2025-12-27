using KaydenTools.Models.SnapSplit.Entities;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 費用 Repository 介面
/// </summary>
public interface IExpenseRepository : IRepository<Expense>
{
    /// <summary>
    /// 根據 ID 取得費用（包含付款者與分攤者資料）
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Expense?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 取得指定帳單的所有費用
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<Expense>> GetByBillIdAsync(Guid billId, CancellationToken ct = default);

    /// <summary>
    /// 取得指定帳單的所有費用（包含付款者與分攤者資料）
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<Expense>> GetByBillIdWithDetailsAsync(Guid billId, CancellationToken ct = default);
}
