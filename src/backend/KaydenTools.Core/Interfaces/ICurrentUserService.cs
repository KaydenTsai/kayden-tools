namespace KaydenTools.Core.Interfaces;

/// <summary>
/// 當前使用者服務介面
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// 當前使用者 ID
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// 當前使用者電子郵件
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// 是否已驗證
    /// </summary>
    bool IsAuthenticated { get; }
}
