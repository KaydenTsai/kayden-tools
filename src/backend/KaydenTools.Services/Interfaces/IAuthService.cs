using Kayden.Commons.Common;
using KaydenTools.Models.Shared.Dtos;
using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Services.Interfaces;

/// <summary>
/// 身份驗證服務
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 根據 ID 取得使用者
    /// </summary>
    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    /// <summary>
    /// 使用 LINE 登入
    /// </summary>
    /// <param name="code">LINE 授權碼</param>
    /// <param name="state">防 CSRF 狀態碼</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<AuthResultDto>> LoginWithLineAsync(string code, string? state, CancellationToken ct = default);

    /// <summary>
    /// 使用 Google 登入
    /// </summary>
    /// <param name="code">Google 授權碼</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<AuthResultDto>> LoginWithGoogleAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// 刷新存取令牌
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="ct">取消令牌</param>
    Task<Result<AuthResultDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// 登出（撤銷刷新令牌）
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="ct">取消令牌</param>
    Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// 撤銷使用者的所有刷新令牌
    /// </summary>
    /// <param name="userId">使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    Task<Result> RevokeAllTokensAsync(Guid userId, CancellationToken ct = default);
}
