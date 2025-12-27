namespace KaydenTools.Core.Common;

/// <summary>
/// API 錯誤碼常數
/// </summary>
public static class ErrorCodes
{
    #region General

    /// <summary>內部錯誤</summary>
    public const string InternalError = "INTERNAL_ERROR";

    /// <summary>驗證錯誤</summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>資源未找到</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>未授權</summary>
    public const string Unauthorized = "UNAUTHORIZED";

    /// <summary>禁止存取</summary>
    public const string Forbidden = "FORBIDDEN";

    /// <summary>錯誤的請求</summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>資源衝突</summary>
    public const string Conflict = "CONFLICT";

    #endregion

    #region Auth

    /// <summary>無效的憑證</summary>
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    /// <summary>令牌已過期</summary>
    public const string TokenExpired = "TOKEN_EXPIRED";

    /// <summary>無效的令牌</summary>
    public const string InvalidToken = "INVALID_TOKEN";

    #endregion

    #region SnapSplit

    /// <summary>帳單未找到</summary>
    public const string BillNotFound = "BILL_NOT_FOUND";

    /// <summary>成員未找到</summary>
    public const string MemberNotFound = "MEMBER_NOT_FOUND";

    /// <summary>費用未找到</summary>
    public const string ExpenseNotFound = "EXPENSE_NOT_FOUND";

    /// <summary>無效的分享碼</summary>
    public const string InvalidShareCode = "INVALID_SHARE_CODE";

    #endregion

    #region UrlShortener

    /// <summary>短網址未找到</summary>
    public const string ShortUrlNotFound = "SHORT_URL_NOT_FOUND";

    /// <summary>無效的網址格式</summary>
    public const string InvalidUrlFormat = "INVALID_URL_FORMAT";

    /// <summary>短碼已被使用</summary>
    public const string ShortCodeInUse = "SHORT_CODE_IN_USE";

    /// <summary>短網址已過期</summary>
    public const string ShortUrlExpired = "SHORT_URL_EXPIRED";

    /// <summary>短網址已停用</summary>
    public const string ShortUrlDisabled = "SHORT_URL_DISABLED";

    /// <summary>無效的短碼格式</summary>
    public const string InvalidShortCode = "INVALID_SHORT_CODE";

    /// <summary>過期時間超出限制</summary>
    public const string ExpirationExceeded = "EXPIRATION_EXCEEDED";

    #endregion
}
