using System.ComponentModel.DataAnnotations;

namespace KaydenTools.Core.Configuration.Settings;

/// <summary>
/// JWT 設定
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// JWT 密鑰
    /// </summary>
    [SettingProperty("Jwt:Secret")]
    [Required(ErrorMessage = "JWT secret is required.")]
    [MinLength(32, ErrorMessage = "JWT secret must be at least 32 characters.")]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// JWT 發行者
    /// </summary>
    [SettingProperty("Jwt:Issuer")]
    [Required(ErrorMessage = "JWT issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// JWT 受眾
    /// </summary>
    [SettingProperty("Jwt:Audience")]
    [Required(ErrorMessage = "JWT audience is required.")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// 存取令牌過期分鐘數
    /// </summary>
    [SettingProperty("Jwt:AccessTokenExpirationMinutes", Required = false, DefaultValue = 15)]
    [Range(1, 1440, ErrorMessage = "AccessTokenExpirationMinutes must be between 1 and 1440.")]
    public int AccessTokenExpirationMinutes { get; set; }

    /// <summary>
    /// 刷新令牌過期天數
    /// </summary>
    [SettingProperty("Jwt:RefreshTokenExpirationDays", Required = false, DefaultValue = 7)]
    [Range(1, 90, ErrorMessage = "RefreshTokenExpirationDays must be between 1 and 90.")]
    public int RefreshTokenExpirationDays { get; set; }
}
