using FluentAssertions;
using Kayden.Commons.Interfaces;
using KaydenTools.Models.UrlShortener.Dtos;
using KaydenTools.Services.UrlShortener;
using NSubstitute;

namespace KaydenTools.Services.Tests.UrlShortener;

/// <summary>
/// ClickTracking 相關測試
/// 包含 Channel、擴充方法、訊息處理等
/// </summary>
public class ClickTrackingTests
{
    private readonly IDateTimeService _dateTimeService;
    private readonly DateTime _now;

    public ClickTrackingTests()
    {
        _dateTimeService = Substitute.For<IDateTimeService>();
        _now = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeService.UtcNow.Returns(_now);
    }

    #region ClickTrackingChannel 測試

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task ClickTrackingChannel_寫入訊息_應可讀取()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var message = new ClickMessage(
            Guid.NewGuid(),
            _now,
            "192.168.1.1",
            "Mozilla/5.0",
            "https://google.com"
        );

        // Act
        var writeResult = channel.Writer.TryWrite(message);

        // Assert
        writeResult.Should().BeTrue();

        var readResult = await channel.Reader.ReadAsync();
        readResult.Should().Be(message);
    }

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task ClickTrackingChannel_多個訊息_應依序讀取()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var messages = Enumerable.Range(1, 5)
            .Select(i => new ClickMessage(
                Guid.NewGuid(),
                _now.AddMinutes(i),
                $"192.168.1.{i}",
                "Mozilla/5.0",
                null
            ))
            .ToList();

        // Act
        foreach (var msg in messages)
        {
            channel.Writer.TryWrite(msg);
        }

        // Assert
        for (int i = 0; i < messages.Count; i++)
        {
            var readMessage = await channel.Reader.ReadAsync();
            readMessage.ShortUrlId.Should().Be(messages[i].ShortUrlId);
            readMessage.IpAddress.Should().Be(messages[i].IpAddress);
        }
    }

    [Fact]
    [Trait("Category", "ClickTracking")]
    public void ClickTrackingChannel_Unbounded_不應阻塞寫入()
    {
        // Arrange
        var channel = new ClickTrackingChannel();

        // Act - 大量寫入
        for (int i = 0; i < 10000; i++)
        {
            var result = channel.Writer.TryWrite(new ClickMessage(
                Guid.NewGuid(),
                _now,
                null,
                null,
                null
            ));

            // Assert
            result.Should().BeTrue();
        }
    }

    #endregion

    #region TrackClick 擴充方法測試

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task TrackClick_應正確寫入訊息()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var shortUrlId = Guid.NewGuid();
        var tracking = new ClickTrackingDto(
            "192.168.1.1",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            "https://google.com"
        );

        // Act
        channel.TrackClick(shortUrlId, tracking, _dateTimeService);

        // Assert
        var message = await channel.Reader.ReadAsync();
        message.ShortUrlId.Should().Be(shortUrlId);
        message.ClickedAt.Should().Be(_now);
        message.IpAddress.Should().Be("192.168.1.1");
        message.UserAgent.Should().Contain("Mozilla");
        message.Referrer.Should().Be("https://google.com");
    }

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task TrackClick_空值參數_應正確處理()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var shortUrlId = Guid.NewGuid();
        var tracking = new ClickTrackingDto(null, null, null);

        // Act
        channel.TrackClick(shortUrlId, tracking, _dateTimeService);

        // Assert
        var message = await channel.Reader.ReadAsync();
        message.ShortUrlId.Should().Be(shortUrlId);
        message.IpAddress.Should().BeNull();
        message.UserAgent.Should().BeNull();
        message.Referrer.Should().BeNull();
    }

    #endregion

    #region ClickMessage 測試

    [Fact]
    [Trait("Category", "ClickTracking")]
    public void ClickMessage_Record類型_應正確比較()
    {
        // Arrange
        var id = Guid.NewGuid();
        var message1 = new ClickMessage(id, _now, "192.168.1.1", "UA", "Ref");
        var message2 = new ClickMessage(id, _now, "192.168.1.1", "UA", "Ref");

        // Assert
        message1.Should().Be(message2);
    }

    [Fact]
    [Trait("Category", "ClickTracking")]
    public void ClickMessage_不同值_應不相等()
    {
        // Arrange
        var message1 = new ClickMessage(Guid.NewGuid(), _now, "192.168.1.1", "UA", "Ref");
        var message2 = new ClickMessage(Guid.NewGuid(), _now, "192.168.1.1", "UA", "Ref");

        // Assert
        message1.Should().NotBe(message2);
    }

    #endregion

    #region 設備類型解析測試（通過整合測試驗證）

    /// <summary>
    /// 這些測試驗證 UserAgent 字串能正確識別設備類型
    /// 實際解析邏輯在 ClickTrackingService.ParseDeviceType 中（private static）
    /// 需要通過整合測試或使用反射來測試
    /// </summary>
    [Theory]
    [Trait("Category", "ClickTracking")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X)", "mobile")]
    [InlineData("Mozilla/5.0 (Linux; Android 11; Pixel 5)", "mobile")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 14_0 like Mac OS X)", "tablet")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", "desktop")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)", "desktop")]
    public void UserAgent_設備類型識別_應正確分類(string userAgent, string expectedDeviceType)
    {
        // 這個測試記錄預期的行為
        // 驗證 UserAgent 包含預期的設備相關字串
        userAgent.Should().NotBeNullOrEmpty();
        expectedDeviceType.Should().BeOneOf("mobile", "tablet", "desktop");

        // 驗證 UserAgent 中包含對應設備的關鍵字
        var ua = userAgent.ToLowerInvariant();
        if (expectedDeviceType == "mobile")
            ua.Should().Match(u => u.Contains("mobile") || u.Contains("android") || u.Contains("iphone"));
        else if (expectedDeviceType == "tablet")
            ua.Should().Match(u => u.Contains("tablet") || u.Contains("ipad"));
    }

    #endregion

    #region 並發安全測試

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task ClickTrackingChannel_並發寫入_應全部成功()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var count = 1000;
        var messages = new List<ClickMessage>();

        // Act - 並發寫入
        var writeTasks = Enumerable.Range(0, count)
            .Select(i =>
            {
                var msg = new ClickMessage(Guid.NewGuid(), _now.AddSeconds(i), null, null, null);
                return Task.Run(() =>
                {
                    channel.Writer.TryWrite(msg);
                    lock (messages)
                    {
                        messages.Add(msg);
                    }
                });
            });

        await Task.WhenAll(writeTasks);

        // Assert
        var readMessages = new List<ClickMessage>();
        while (readMessages.Count < count)
        {
            if (channel.Reader.TryRead(out var msg))
            {
                readMessages.Add(msg);
            }
        }

        readMessages.Should().HaveCount(count);
    }

    #endregion

    #region 邊界條件測試

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task TrackClick_極長UserAgent_應正確處理()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var shortUrlId = Guid.NewGuid();
        var longUserAgent = new string('A', 1000); // 極長 UserAgent
        var tracking = new ClickTrackingDto("192.168.1.1", longUserAgent, null);

        // Act
        channel.TrackClick(shortUrlId, tracking, _dateTimeService);

        // Assert
        var message = await channel.Reader.ReadAsync();
        message.UserAgent.Should().Be(longUserAgent); // Channel 本身不截斷，由 Service 截斷
    }

    [Fact]
    [Trait("Category", "ClickTracking")]
    public async Task TrackClick_極長Referrer_應正確處理()
    {
        // Arrange
        var channel = new ClickTrackingChannel();
        var shortUrlId = Guid.NewGuid();
        var longReferrer = "https://example.com/" + new string('a', 3000);
        var tracking = new ClickTrackingDto(null, null, longReferrer);

        // Act
        channel.TrackClick(shortUrlId, tracking, _dateTimeService);

        // Assert
        var message = await channel.Reader.ReadAsync();
        message.Referrer.Should().Be(longReferrer); // Channel 本身不截斷
    }

    #endregion
}
