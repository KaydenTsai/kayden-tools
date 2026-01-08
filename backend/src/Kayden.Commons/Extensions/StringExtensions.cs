using System.Security.Cryptography;
using System.Text;

namespace Kayden.Commons.Extensions;

/// <summary>
/// 字串擴充方法
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 將字串轉為 SHA256 雜湊
    /// </summary>
    public static string ToSha256(this string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// 截斷字串至指定長度
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// 檢查字串是否為 null 或空字串
    /// </summary>
    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// 檢查字串是否為 null、空字串或僅包含空白字元
    /// </summary>
    public static bool IsNullOrWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}
