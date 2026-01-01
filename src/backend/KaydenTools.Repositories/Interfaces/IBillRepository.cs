using KaydenTools.Models.SnapSplit.Entities;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 帳單 Repository 介面
/// </summary>
public interface IBillRepository : IRepository<Bill>
{
    /// <summary>
    /// 根據 ID 取得帳單（包含成員、費用、結算等詳細資料）
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Bill?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 根據分享碼取得帳單
    /// </summary>
    /// <param name="shareCode">分享碼</param>
    /// <param name="ct">取消令牌</param>
    Task<Bill?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default);

    /// <summary>
    /// 取得指定擁有者的所有帳單
    /// </summary>
    /// <param name="ownerId">擁有者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<Bill>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// 取得使用者參與的所有帳單（透過 Member.LinkedUserId）
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<Bill>> GetByLinkedUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 取得帳單的當前版本（直接查詢資料庫，不使用快取）
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<long?> GetCurrentVersionAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 根據 ID 取得帳單並鎖定該列（使用 SELECT FOR UPDATE）
    /// 用於防止並發修改
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Bill?> GetByIdWithLockAsync(Guid id, CancellationToken ct = default);
}
