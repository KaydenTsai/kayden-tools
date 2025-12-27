using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.UrlShortener.Entities;

/// <summary>
/// 短網址實體
/// </summary>
public class ShortUrl : IEntity, IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; }

    /// <summary>
    /// 原始 URL（支援長網址如 LZString 壓縮資料）
    /// </summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>
    /// 短碼（如 abc123）
    /// </summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>
    /// 擁有者 ID（匿名建立為 null）
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// 過期時間（null 表示永久有效）
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 點擊次數（去正規化欄位，使用 atomic increment 更新）
    /// </summary>
    public long ClickCount { get; set; }

    /// <summary>
    /// 是否啟用
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    // Navigation
    public ICollection<UrlClick> Clicks { get; set; } = new List<UrlClick>();
}