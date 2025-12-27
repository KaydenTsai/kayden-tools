using Kayden.Commons.Interfaces;
using KaydenTools.Models.Shared.Enums;

namespace KaydenTools.Models.Shared.Entities;

/// <summary>
/// 使用者實體
/// </summary>
public class User : IEntity, IAuditableEntity, ISoftDeletable
{
    /// <summary>
    /// 使用者 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 電子郵件
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// 頭像網址
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// 主要登入方式
    /// </summary>
    public AuthProvider PrimaryProvider { get; set; }

    /// <summary>
    /// LINE 使用者 ID
    /// </summary>
    public string? LineUserId { get; set; }

    /// <summary>
    /// LINE 頭像網址
    /// </summary>
    public string? LinePictureUrl { get; set; }

    /// <summary>
    /// Google 使用者 ID
    /// </summary>
    public string? GoogleUserId { get; set; }

    /// <summary>
    /// Google 頭像網址
    /// </summary>
    public string? GooglePictureUrl { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// 刷新令牌集合
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
