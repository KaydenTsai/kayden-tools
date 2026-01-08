namespace KaydenTools.Core.Configuration;

/// <summary>
/// 設定屬性標記，用於標記設定類別的屬性與設定檔的對應關係
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SettingPropertyAttribute : Attribute
{
    /// <summary>
    /// 建立設定屬性標記
    /// </summary>
    /// <param name="key">設定檔中的鍵值路徑</param>
    public SettingPropertyAttribute(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 設定檔中的鍵值路徑
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 是否為必要設定
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 預設值
    /// </summary>
    public object? DefaultValue { get; set; }
}
