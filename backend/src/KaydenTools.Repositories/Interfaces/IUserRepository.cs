using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 使用者 Repository 介面
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// 根據電子郵件取得使用者
    /// </summary>
    /// <param name="email">電子郵件</param>
    /// <param name="ct">取消令牌</param>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// 根據 LINE 使用者 ID 取得使用者
    /// </summary>
    /// <param name="lineUserId">LINE 使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<User?> GetByLineUserIdAsync(string lineUserId, CancellationToken ct = default);

    /// <summary>
    /// 根據 Google 使用者 ID 取得使用者
    /// </summary>
    /// <param name="googleUserId">Google 使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<User?> GetByGoogleUserIdAsync(string googleUserId, CancellationToken ct = default);
}
