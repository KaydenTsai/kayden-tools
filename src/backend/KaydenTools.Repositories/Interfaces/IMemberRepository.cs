using KaydenTools.Models.SnapSplit.Entities;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 成員 Repository 介面
/// </summary>
public interface IMemberRepository : IRepository<Member>
{
    /// <summary>
    /// 取得指定帳單的所有成員
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<Member>> GetByBillIdAsync(Guid billId, CancellationToken ct = default);

    /// <summary>
    /// 取得帳單的下一個顯示順序編號
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<int> GetNextDisplayOrderAsync(Guid billId, CancellationToken ct = default);
}
