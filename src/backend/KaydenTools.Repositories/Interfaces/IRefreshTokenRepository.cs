using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 刷新令牌 Repository 介面
/// </summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>
    /// 根據令牌字串取得刷新令牌
    /// </summary>
    /// <param name="token">令牌字串</param>
    /// <param name="ct">取消令牌</param>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// 取得使用者所有有效的刷新令牌
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// 撤銷使用者的所有刷新令牌
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
