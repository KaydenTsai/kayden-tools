namespace KaydenTools.Core.Configuration.Settings;

/// <summary>
/// LINE 登入設定
/// </summary>
public class LineLoginSettings
{
    /// <summary>
    /// LINE Channel ID
    /// </summary>
    [SettingProperty("LineLogin:ChannelId", Required = false)]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// LINE Channel Secret
    /// </summary>
    [SettingProperty("LineLogin:ChannelSecret", Required = false)]
    public string ChannelSecret { get; set; } = string.Empty;

    /// <summary>
    /// 回調網址
    /// </summary>
    [SettingProperty("LineLogin:CallbackUrl", Required = false)]
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// LIFF ID
    /// </summary>
    [SettingProperty("LineLogin:LiffId", Required = false)]
    public string? LiffId { get; set; }

    /// <summary>
    /// 是否已設定
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(ChannelId) && !string.IsNullOrEmpty(ChannelSecret);
}
