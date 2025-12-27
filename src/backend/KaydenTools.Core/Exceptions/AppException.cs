using System.Net;

namespace KaydenTools.Core.Exceptions;

/// <summary>
/// 應用程式例外基底類別
/// </summary>
public class AppException : Exception
{
    /// <summary>
    /// 錯誤碼
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// HTTP 狀態碼
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// 詳細資訊
    /// </summary>
    public object? Details { get; }

    /// <summary>
    /// 建立應用程式例外
    /// </summary>
    public AppException(
        string code,
        string message,
        HttpStatusCode statusCode = HttpStatusCode.BadRequest,
        object? details = null) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }
}

/// <summary>
/// 資源未找到例外
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string code, string message)
        : base(code, message, HttpStatusCode.NotFound)
    {
    }

    /// <summary>
    /// 建立指定類型的未找到例外
    /// </summary>
    public static NotFoundException For<T>(object id) =>
        new($"{typeof(T).Name.ToUpperInvariant()}_NOT_FOUND", $"{typeof(T).Name} with id '{id}' was not found.");
}

/// <summary>
/// 驗證例外
/// </summary>
public class ValidationException : AppException
{
    public ValidationException(string message, object? details = null)
        : base("VALIDATION_ERROR", message, HttpStatusCode.BadRequest, details)
    {
    }
}

/// <summary>
/// 未授權例外
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized")
        : base("UNAUTHORIZED", message, HttpStatusCode.Unauthorized)
    {
    }
}

/// <summary>
/// 禁止存取例外
/// </summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden")
        : base("FORBIDDEN", message, HttpStatusCode.Forbidden)
    {
    }
}

/// <summary>
/// 資源衝突例外
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string code, string message)
        : base(code, message, HttpStatusCode.Conflict)
    {
    }
}
