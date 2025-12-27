using System.ComponentModel.DataAnnotations;

namespace KaydenTools.Core.Configuration.Settings;

/// <summary>
/// 資料庫連線設定
/// </summary>
public class DatabaseSettings
{
    /// <summary>
    /// 資料庫連線字串
    /// </summary>
    [SettingProperty("Database:ConnectionString")]
    [Required(ErrorMessage = "Database connection string is required.")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 最大重試次數
    /// </summary>
    [SettingProperty("Database:MaxRetryCount", Required = false, DefaultValue = 3)]
    [Range(1, 10, ErrorMessage = "MaxRetryCount must be between 1 and 10.")]
    public int MaxRetryCount { get; set; }

    /// <summary>
    /// 命令逾時時間（秒）
    /// </summary>
    [SettingProperty("Database:CommandTimeout", Required = false, DefaultValue = 30)]
    [Range(5, 300, ErrorMessage = "CommandTimeout must be between 5 and 300 seconds.")]
    public int CommandTimeout { get; set; }
}
