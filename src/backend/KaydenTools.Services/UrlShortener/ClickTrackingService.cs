using System.Threading.Channels;
using Kayden.Commons.Interfaces;
using KaydenTools.Models.UrlShortener.Dtos;
using KaydenTools.Models.UrlShortener.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KaydenTools.Services.UrlShortener;

/// <summary>
/// 點擊追蹤訊息
/// </summary>
public record ClickMessage(
    Guid ShortUrlId,
    DateTime ClickedAt,
    string? IpAddress,
    string? UserAgent,
    string? Referrer
);

/// <summary>
/// 點擊追蹤 Channel（用於跨服務溝通）
/// </summary>
public class ClickTrackingChannel
{
    private readonly Channel<ClickMessage> _channel;

    public ClickTrackingChannel()
    {
        // Unbounded channel - 不阻塞寫入
        _channel = Channel.CreateUnbounded<ClickMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<ClickMessage> Writer => _channel.Writer;
    public ChannelReader<ClickMessage> Reader => _channel.Reader;
}

/// <summary>
/// 背景服務：從 Channel 讀取點擊事件並寫入資料庫
/// </summary>
public class ClickTrackingService : BackgroundService
{
    private readonly ClickTrackingChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClickTrackingService> _logger;

    public ClickTrackingService(
        ClickTrackingChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ClickTrackingService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClickTrackingService started");

        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessClickAsync(message, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing click for ShortUrlId: {ShortUrlId}", message.ShortUrlId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常關閉
        }

        _logger.LogInformation("ClickTrackingService stopped");
    }

    private async Task ProcessClickAsync(ClickMessage message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var click = new UrlClick
        {
            Id = Guid.NewGuid(),
            ShortUrlId = message.ShortUrlId,
            ClickedAt = message.ClickedAt,
            IpAddress = message.IpAddress,
            UserAgent = TruncateString(message.UserAgent, 512),
            Referrer = TruncateString(message.Referrer, 2048),
            DeviceType = ParseDeviceType(message.UserAgent)
        };

        await unitOfWork.UrlClicks.AddAsync(click, ct);
        await unitOfWork.ShortUrls.IncrementClickCountAsync(message.ShortUrlId, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string? TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? ParseDeviceType(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return null;
        }

        var ua = userAgent.ToLowerInvariant();
        if (ua.Contains("mobile") || ua.Contains("android") || ua.Contains("iphone"))
        {
            return "mobile";
        }
        if (ua.Contains("tablet") || ua.Contains("ipad"))
        {
            return "tablet";
        }
        return "desktop";
    }
}

/// <summary>
/// 擴充方法：用於追蹤點擊（Fire and Forget）
/// </summary>
public static class ClickTrackingExtensions
{
    /// <summary>
    /// 追蹤點擊（非同步，不阻塞）
    /// </summary>
    public static void TrackClick(
        this ClickTrackingChannel channel,
        Guid shortUrlId,
        ClickTrackingDto tracking,
        IDateTimeService dateTimeService)
    {
        var message = new ClickMessage(
            shortUrlId,
            dateTimeService.UtcNow,
            tracking.IpAddress,
            tracking.UserAgent,
            tracking.Referrer
        );

        // TryWrite 不阻塞，如果 Channel 已滿則丟棄（Unbounded 不會發生）
        if (!channel.Writer.TryWrite(message))
        {
            // Log warning if needed
        }
    }
}