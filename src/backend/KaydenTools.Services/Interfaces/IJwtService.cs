using System.Security.Claims;
using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Services.Interfaces;

/// <summary>
/// JWT 令牌服務
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// 產生存取令牌
    /// </summary>
    /// <param name="user">使用者實體</param>
    string GenerateAccessToken(User user);

    /// <summary>
    /// 產生刷新令牌
    /// </summary>
    string GenerateRefreshToken();

    /// <summary>
    /// 驗證令牌並回傳 ClaimsPrincipal
    /// </summary>
    /// <param name="token">JWT 令牌</param>
    ClaimsPrincipal? ValidateToken(string token);

    /// <summary>
    /// 取得存取令牌過期分鐘數
    /// </summary>
    int GetAccessTokenExpirationMinutes();

    /// <summary>
    /// 取得刷新令牌過期天數
    /// </summary>
    int GetRefreshTokenExpirationDays();
}
