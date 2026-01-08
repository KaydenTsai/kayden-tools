using System.Net;
using System.Text.Json;
using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Exceptions;
using ValidationException = FluentValidation.ValidationException;

namespace KaydenTools.Api.Middleware;

/// <summary>
/// 全域例外處理中介軟體
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly RequestDelegate _next;

    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="next">下一個中介軟體</param>
    /// <param name="logger">日誌記錄器</param>
    /// <param name="env">主機環境</param>
    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// 執行中介軟體
    /// </summary>
    /// <param name="context">HTTP 上下文</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            AppException appEx => HandleAppException(appEx),
            ValidationException fluentEx => HandleFluentValidationException(fluentEx),
            _ => HandleUnknownException(exception)
        };

        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
        else
            _logger.LogWarning("Handled exception: {Code} - {Message}", response.Error?.Code, response.Error?.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private (HttpStatusCode, ApiResponse) HandleAppException(AppException ex)
    {
        return (ex.StatusCode, ApiResponse.Fail(ex.Code, ex.Message, ex.Details));
    }

    private (HttpStatusCode, ApiResponse) HandleFluentValidationException(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        return (HttpStatusCode.BadRequest, ApiResponse.Fail(
            ErrorCodes.ValidationError,
            "One or more validation errors occurred.",
            errors
        ));
    }

    private (HttpStatusCode, ApiResponse) HandleUnknownException(Exception ex)
    {
        var message = _env.IsDevelopment()
            ? ex.Message
            : "An unexpected error occurred. Please try again later.";

        var details = _env.IsDevelopment()
            ? new {
                stackTrace = ex.StackTrace,
                innerException = ex.InnerException?.Message,
                innerStackTrace = ex.InnerException?.StackTrace,
                innerInnerException = ex.InnerException?.InnerException?.Message
            }
            : null;

        return (HttpStatusCode.InternalServerError, ApiResponse.Fail(
            ErrorCodes.InternalError,
            message,
            details
        ));
    }
}

/// <summary>
/// 全域例外處理中介軟體擴充方法
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>
    /// 使用全域例外處理中介軟體
    /// </summary>
    /// <param name="app">應用程式建構器</param>
    /// <returns>應用程式建構器</returns>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
