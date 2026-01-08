using KaydenTools.Models.SnapSplit.Entities;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 已結清轉帳 Repository 介面
/// </summary>
public interface ISettledTransferRepository
{
    /// <summary>
    /// 取得指定帳單的所有已結清轉帳記錄
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<SettledTransfer>> GetByBillIdAsync(Guid billId, CancellationToken ct = default);

    /// <summary>
    /// 根據複合主鍵取得已結清轉帳記錄
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="fromMemberId">付款方成員 ID</param>
    /// <param name="toMemberId">收款方成員 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<SettledTransfer?> GetByKeyAsync(Guid billId, Guid fromMemberId, Guid toMemberId,
        CancellationToken ct = default);

    /// <summary>
    /// 新增已結清轉帳記錄
    /// </summary>
    /// <param name="entity">實體</param>
    Task AddAsync(SettledTransfer entity);

    /// <summary>
    /// 刪除已結清轉帳記錄
    /// </summary>
    /// <param name="entity">實體</param>
    void Remove(SettledTransfer entity);

    /// <summary>
    /// 刪除指定帳單的所有已結清轉帳記錄
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task RemoveByBillIdAsync(Guid billId, CancellationToken ct = default);
}
