using Bogus;

namespace KaydenTools.TestUtilities.Common;

/// <summary>
/// 共用的測試假資料產生器
/// 避免每個 Builder 都建立自己的 Faker 實例
/// </summary>
public static class TestFaker
{
    /// <summary>
    /// 共用的 Faker 實例（繁體中文）
    /// </summary>
    public static Faker Instance { get; } = new("zh_TW");

    /// <summary>
    /// 產生隨機價格
    /// </summary>
    public static decimal RandomPrice(decimal min = 10m, decimal max = 10000m)
        => Math.Round(Instance.Random.Decimal(min, max), 2);

    /// <summary>
    /// 產生隨機百分比（0-100）
    /// </summary>
    public static decimal RandomPercent(decimal min = 0m, decimal max = 20m)
        => Math.Round(Instance.Random.Decimal(min, max), 1);

    /// <summary>
    /// 產生隨機分享碼（8 字元大寫英數）
    /// </summary>
    public static string GenerateShareCode()
        => Instance.Random.AlphaNumeric(8).ToUpper();

    /// <summary>
    /// 產生隨機名字
    /// </summary>
    public static string RandomName()
        => Instance.Name.FirstName();

    /// <summary>
    /// 產生隨機商品名稱
    /// </summary>
    public static string RandomProductName()
        => Instance.Commerce.ProductName();
}
