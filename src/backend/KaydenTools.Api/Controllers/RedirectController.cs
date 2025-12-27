using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Models.UrlShortener.Dtos;
using KaydenTools.Services.Interfaces;
using KaydenTools.Services.UrlShortener;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 短網址轉址
/// </summary>
[ApiController]
[AllowAnonymous]
public class RedirectController : ControllerBase
{
    private readonly IShortUrlService _shortUrlService;
    private readonly ClickTrackingChannel _clickChannel;
    private readonly IDateTimeService _dateTimeService;

    public RedirectController(
        IShortUrlService shortUrlService,
        ClickTrackingChannel clickChannel,
        IDateTimeService dateTimeService)
    {
        _shortUrlService = shortUrlService;
        _clickChannel = clickChannel;
        _dateTimeService = dateTimeService;
    }

    /// <summary>
    /// 轉址到原始 URL
    /// </summary>
    /// <remarks>
    /// 使用 302 Found（非 301）避免瀏覽器快取導致 Analytics 失真
    /// </remarks>
    [HttpGet("/{shortCode}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Redirect(string shortCode, CancellationToken ct)
    {
        // 解析短碼
        var result = await _shortUrlService.ResolveAsync(shortCode, ct);

        if (result.IsFailure)
        {
            // 判斷錯誤類型
            if (result.Error.Code == ErrorCodes.ShortUrlExpired ||
                result.Error.Code == ErrorCodes.ShortUrlDisabled)
            {
                return StatusCode(StatusCodes.Status410Gone, new { error = result.Error.Message });
            }
            return NotFound(new { error = result.Error.Message });
        }

        // 非同步追蹤點擊（Fire and Forget）
        var shortUrl = await _shortUrlService.GetByShortCodeAsync(shortCode, ct);
        if (shortUrl.IsSuccess)
        {
            var tracking = new ClickTrackingDto(
                GetClientIpAddress(),
                Request.Headers.UserAgent.ToString(),
                Request.Headers.Referer.ToString()
            );
            _clickChannel.TrackClick(shortUrl.Value!.Id, tracking, _dateTimeService);
        }

        // 使用 302 Found 轉址（不快取）
        return Redirect(result.Value!);
    }

    private string? GetClientIpAddress()
    {
        // 檢查 X-Forwarded-For（反向代理/負載均衡器後）
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }

        // 直接連線的 IP
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}