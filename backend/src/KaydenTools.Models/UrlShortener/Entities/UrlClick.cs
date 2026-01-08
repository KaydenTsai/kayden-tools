using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.UrlShortener.Entities;

/// <summary>
/// 短網址點擊記錄
/// </summary>
public class UrlClick : IEntity
{
    /// <summary>
    /// 關聯的短網址 ID
    /// </summary>
    public Guid ShortUrlId { get; set; }

    /// <summary>
    /// 點擊時間
    /// </summary>
    public DateTime ClickedAt { get; set; }

    /// <summary>
    /// 客戶端 IP（支援 IPv6，最長 45 字元）
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 瀏覽器 User-Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// HTTP Referer
    /// </summary>
    public string? Referrer { get; set; }

    /// <summary>
    /// 裝置類型：mobile / desktop / tablet
    /// </summary>
    public string? DeviceType { get; set; }

    // Navigation
    public ShortUrl ShortUrl { get; set; } = null!;

    #region IEntity Members

    public Guid Id { get; set; }

    #endregion
}
