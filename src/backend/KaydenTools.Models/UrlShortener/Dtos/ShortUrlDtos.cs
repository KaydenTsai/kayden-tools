namespace KaydenTools.Models.UrlShortener.Dtos;

/// <summary>
/// 建立短網址請求
/// </summary>
public record CreateShortUrlDto(
    string OriginalUrl,
    string? CustomCode = null,
    DateTime? ExpiresAt = null
);

/// <summary>
/// 更新短網址請求
/// </summary>
public record UpdateShortUrlDto(
    string? OriginalUrl = null,
    DateTime? ExpiresAt = null,
    bool? IsActive = null
);

/// <summary>
/// 短網址詳細資訊
/// </summary>
public record ShortUrlDto(
    Guid Id,
    string OriginalUrl,
    string ShortCode,
    string ShortUrl,
    long ClickCount,
    DateTime? ExpiresAt,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// 短網址摘要（用於列表）
/// </summary>
public record ShortUrlSummaryDto(
    Guid Id,
    string ShortCode,
    string ShortUrl,
    long ClickCount,
    DateTime CreatedAt,
    bool IsActive,
    bool IsExpired
);

/// <summary>
/// 點擊記錄
/// </summary>
public record UrlClickDto(
    Guid Id,
    DateTime ClickedAt,
    string? IpAddress,
    string? UserAgent,
    string? Referrer,
    string? DeviceType
);

/// <summary>
/// 短網址統計資訊
/// </summary>
public record UrlStatsDto(
    Guid ShortUrlId,
    string ShortCode,
    long TotalClicks,
    DateTime? LastClickAt,
    IReadOnlyList<ClicksByDateDto> ClicksByDate,
    IReadOnlyList<ClicksByReferrerDto> TopReferrers,
    IReadOnlyList<ClicksByDeviceDto> DeviceBreakdown
);

public record ClicksByDateDto(DateOnly Date, int Count);
public record ClicksByReferrerDto(string Referrer, int Count);
public record ClicksByDeviceDto(string DeviceType, int Count);

/// <summary>
/// 點擊追蹤資訊（用於背景處理）
/// </summary>
public record ClickTrackingDto(
    string? IpAddress,
    string? UserAgent,
    string? Referrer
);