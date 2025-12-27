namespace KaydenTools.Core.Configuration.Settings;

/// <summary>
/// Google 登入設定
/// </summary>
public class GoogleLoginSettings
{
    /// <summary>
    /// Google Client ID
    /// </summary>
    [SettingProperty("GoogleLogin:ClientId", Required = false)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Google Client Secret
    /// </summary>
    [SettingProperty("GoogleLogin:ClientSecret", Required = false)]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// 回調網址
    /// </summary>
    [SettingProperty("GoogleLogin:CallbackUrl", Required = false)]
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// 是否已設定
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);
}
