namespace Kayden.Commons.Common;

/// <summary>
/// API 回應
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 回傳資料
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 錯誤資訊
    /// </summary>
    public ErrorInfo? Error { get; set; }

    /// <summary>
    /// 建立成功回應
    /// </summary>
    public static ApiResponse Ok(object? data = null) => new()
    {
        Success = true,
        Data = data
    };

    /// <summary>
    /// 建立失敗回應
    /// </summary>
    public static ApiResponse Fail(string code, string message, object? details = null) => new()
    {
        Success = false,
        Error = new ErrorInfo { Code = code, Message = message, Details = details }
    };

    /// <summary>
    /// 建立失敗回應
    /// </summary>
    public static ApiResponse Fail(ErrorInfo error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// API 回應
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 回傳資料
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 錯誤資訊
    /// </summary>
    public ErrorInfo? Error { get; set; }

    /// <summary>
    /// 建立成功回應
    /// </summary>
    public static ApiResponse<T> Ok(T? data = default) => new()
    {
        Success = true,
        Data = data
    };

    /// <summary>
    /// 建立失敗回應
    /// </summary>
    public static ApiResponse<T> Fail(string code, string message, object? details = null) => new()
    {
        Success = false,
        Error = new ErrorInfo { Code = code, Message = message, Details = details }
    };
}

/// <summary>
/// 錯誤資訊
/// </summary>
public class ErrorInfo
{
    /// <summary>
    /// 錯誤碼
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 詳細資訊
    /// </summary>
    public object? Details { get; set; }
}
