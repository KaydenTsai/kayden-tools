using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.Shared.Entities;

/// <summary>
/// 刷新令牌實體
/// </summary>
public class RefreshToken : IEntity
{
    /// <summary>
    /// 令牌 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所屬使用者 ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 令牌字串
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 裝置資訊
    /// </summary>
    public string? DeviceInfo { get; set; }

    /// <summary>
    /// 客戶端 IP 位址
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 過期時間
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 撤銷時間
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// 是否已撤銷
    /// </summary>
    public bool IsRevoked => RevokedAt != null;

    /// <summary>
    /// 是否已過期
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// 是否有效（未撤銷且未過期）
    /// </summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    /// <summary>
    /// 所屬使用者
    /// </summary>
    public User User { get; set; } = null!;
}
