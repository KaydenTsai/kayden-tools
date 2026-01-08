namespace KaydenTools.Core.Configuration.Settings;

public class UrlShortenerSettings
{
    /// <summary>
    /// 短網址的基底 URL
    /// </summary>
    [SettingProperty("UrlShortener:BaseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 預設短碼長度（4-12 字元）
    /// </summary>
    [SettingProperty("UrlShortener:DefaultCodeLength", Required = false)]
    public int DefaultCodeLength { get; set; } = 6;

    /// <summary>
    /// 最長有效期限（天數）
    /// </summary>
    [SettingProperty("UrlShortener:MaxTtlDays", Required = false)]
    public int MaxTtlDays { get; set; } = 365;

    /// <summary>
    /// 是否允許匿名建立短網址
    /// </summary>
    [SettingProperty("UrlShortener:AllowAnonymousCreation", Required = false)]
    public bool AllowAnonymousCreation { get; set; } = true;

    /// <summary>
    /// 每位使用者可建立的短網址上限
    /// </summary>
    [SettingProperty("UrlShortener:MaxUrlsPerUser", Required = false)]
    public int MaxUrlsPerUser { get; set; } = 100;

    /// <summary>
    /// Rate Limiting: 每分鐘允許的匿名建立次數
    /// </summary>
    [SettingProperty("UrlShortener:RateLimitPerMinute", Required = false)]
    public int RateLimitPerMinute { get; set; } = 10;
}
