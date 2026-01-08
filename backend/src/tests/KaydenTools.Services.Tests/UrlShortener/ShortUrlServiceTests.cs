using FluentAssertions;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Models.UrlShortener.Dtos;
using KaydenTools.Models.UrlShortener.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.UrlShortener;
using NSubstitute;

namespace KaydenTools.Services.Tests.UrlShortener;

/// <summary>
/// ShortUrlService 單元測試
/// </summary>
public class ShortUrlServiceTests
{
    private readonly ShortUrlService _sut;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeService _dateTimeService;
    private readonly UrlShortenerSettings _settings;
    private readonly DateTime _now;

    public ShortUrlServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _dateTimeService = Substitute.For<IDateTimeService>();

        _now = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeService.UtcNow.Returns(_now);

        _settings = new UrlShortenerSettings
        {
            BaseUrl = "https://short.url",
            DefaultCodeLength = 6,
            MaxTtlDays = 365,
            AllowAnonymousCreation = true,
            MaxUrlsPerUser = 100
        };

        _sut = new ShortUrlService(_unitOfWork, _dateTimeService, _settings);
    }

    #region CreateAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_有效URL_應成功建立短網址()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var dto = new CreateShortUrlDto("https://example.com/long-url");

        _unitOfWork.ShortUrls.ShortCodeExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.CreateAsync(dto, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OriginalUrl.Should().Be("https://example.com/long-url");
        result.Value.ShortCode.Should().NotBeNullOrEmpty();
        result.Value.ShortUrl.Should().StartWith(_settings.BaseUrl);
        result.Value.IsActive.Should().BeTrue();
        result.Value.ClickCount.Should().Be(0);

        await _unitOfWork.ShortUrls.Received(1).AddAsync(Arg.Any<ShortUrl>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_HTTP協議_應成功建立()
    {
        // Arrange
        var dto = new CreateShortUrlDto("http://example.com");

        _unitOfWork.ShortUrls.ShortCodeExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_無效URL格式_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto("not-a-valid-url");

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidUrlFormat);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_FTP協議_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto("ftp://example.com/file");

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidUrlFormat);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_過期時間超過最大限制_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto(
            "https://example.com",
            null,
            _now.AddDays(_settings.MaxTtlDays + 1)
        );

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ExpirationExceeded);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_過期時間在過去_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto(
            "https://example.com",
            null,
            _now.AddDays(-1)
        );

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_有效自訂短碼_應使用自訂短碼()
    {
        // Arrange
        var dto = new CreateShortUrlDto("https://example.com", "my-code");

        _unitOfWork.ShortUrls.ShortCodeExistsAsync("my-code", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be("my-code");
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_自訂短碼包含無效字元_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto("https://example.com", "my code!"); // 空格和驚嘆號無效

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidShortCode);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_自訂短碼太短_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto("https://example.com", "ab"); // 少於 3 字元

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidShortCode);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_自訂短碼太長_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto("https://example.com", new string('a', 21)); // 超過 20 字元

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidShortCode);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_自訂短碼已被使用_應回傳錯誤()
    {
        // Arrange
        var dto = new CreateShortUrlDto("https://example.com", "taken");

        _unitOfWork.ShortUrls.ShortCodeExistsAsync("taken", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortCodeInUse);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task CreateAsync_自訂短碼含底線_應成功()
    {
        // Arrange
        var dto = new CreateShortUrlDto("https://example.com", "my_code");

        _unitOfWork.ShortUrls.ShortCodeExistsAsync("my_code", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.CreateAsync(dto, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be("my_code");
    }

    #endregion

    #region GetByIdAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetByIdAsync_短網址存在_應回傳資料()
    {
        // Arrange
        var id = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OriginalUrl = "https://example.com",
            ShortCode = "abc123",
            ClickCount = 10,
            IsActive = true,
            CreatedAt = _now.AddDays(-1)
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(id);
        result.Value.ShortCode.Should().Be("abc123");
        result.Value.ClickCount.Should().Be(10);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetByIdAsync_短網址不存在_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ShortUrl?)null);

        // Act
        var result = await _sut.GetByIdAsync(id);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlNotFound);
    }

    #endregion

    #region GetByShortCodeAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetByShortCodeAsync_短網址存在_應回傳資料()
    {
        // Arrange
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com",
            ShortCode = "abc123",
            IsActive = true
        };

        _unitOfWork.ShortUrls.GetByShortCodeAsync("abc123", Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.GetByShortCodeAsync("abc123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ShortCode.Should().Be("abc123");
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetByShortCodeAsync_短網址不存在_應回傳錯誤()
    {
        // Arrange
        _unitOfWork.ShortUrls.GetByShortCodeAsync("notexist", Arg.Any<CancellationToken>())
            .Returns((ShortUrl?)null);

        // Act
        var result = await _sut.GetByShortCodeAsync("notexist");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlNotFound);
    }

    #endregion

    #region GetByOwnerIdAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetByOwnerIdAsync_有多個短網址_應回傳全部()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var shortUrls = new List<ShortUrl>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ShortCode = "abc",
                ClickCount = 5,
                IsActive = true,
                ExpiresAt = null,
                CreatedAt = _now.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                ShortCode = "def",
                ClickCount = 10,
                IsActive = false,
                ExpiresAt = _now.AddDays(-1), // 已過期
                CreatedAt = _now.AddDays(-2)
            }
        };

        _unitOfWork.ShortUrls.GetByOwnerIdAsync(ownerId, Arg.Any<CancellationToken>())
            .Returns(shortUrls);

        // Act
        var result = await _sut.GetByOwnerIdAsync(ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(s => s.ShortCode == "abc" && !s.IsExpired);
        result.Value.Should().Contain(s => s.ShortCode == "def" && s.IsExpired);
    }

    #endregion

    #region UpdateAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task UpdateAsync_擁有者更新_應成功()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OriginalUrl = "https://old.com",
            ShortCode = "abc",
            OwnerId = ownerId,
            IsActive = true
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        var dto = new UpdateShortUrlDto("https://new.com", null, false);

        // Act
        var result = await _sut.UpdateAsync(id, dto, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        shortUrl.OriginalUrl.Should().Be("https://new.com");
        shortUrl.IsActive.Should().BeFalse();

        _unitOfWork.ShortUrls.Received(1).Update(shortUrl);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task UpdateAsync_非擁有者更新_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OriginalUrl = "https://example.com",
            OwnerId = ownerId
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        var dto = new UpdateShortUrlDto("https://new.com");

        // Act
        var result = await _sut.UpdateAsync(id, dto, otherId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Forbidden);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task UpdateAsync_無擁有者的短網址_任何人可更新()
    {
        // Arrange
        var id = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OriginalUrl = "https://example.com",
            OwnerId = null // 無擁有者
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        var dto = new UpdateShortUrlDto("https://new.com");

        // Act
        var result = await _sut.UpdateAsync(id, dto, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task UpdateAsync_短網址不存在_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ShortUrl?)null);

        var dto = new UpdateShortUrlDto("https://new.com");

        // Act
        var result = await _sut.UpdateAsync(id, dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlNotFound);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task UpdateAsync_無效URL格式_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OriginalUrl = "https://example.com",
            OwnerId = null
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        var dto = new UpdateShortUrlDto("invalid-url");

        // Act
        var result = await _sut.UpdateAsync(id, dto, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidUrlFormat);
    }

    #endregion

    #region DeleteAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task DeleteAsync_擁有者刪除_應成功()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OwnerId = ownerId
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.DeleteAsync(id, ownerId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.ShortUrls.Received(1).Remove(shortUrl);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task DeleteAsync_非擁有者刪除_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            OwnerId = ownerId
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.DeleteAsync(id, otherId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Forbidden);
        _unitOfWork.ShortUrls.DidNotReceive().Remove(Arg.Any<ShortUrl>());
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task DeleteAsync_短網址不存在_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ShortUrl?)null);

        // Act
        var result = await _sut.DeleteAsync(id, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlNotFound);
    }

    #endregion

    #region ResolveAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task ResolveAsync_有效短網址_應回傳原始URL()
    {
        // Arrange
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/target",
            ShortCode = "abc",
            IsActive = true,
            ExpiresAt = null
        };

        _unitOfWork.ShortUrls.GetByShortCodeAsync("abc", Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.ResolveAsync("abc");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("https://example.com/target");
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task ResolveAsync_短網址不存在_應回傳錯誤()
    {
        // Arrange
        _unitOfWork.ShortUrls.GetByShortCodeAsync("notexist", Arg.Any<CancellationToken>())
            .Returns((ShortUrl?)null);

        // Act
        var result = await _sut.ResolveAsync("notexist");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlNotFound);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task ResolveAsync_短網址已停用_應回傳錯誤()
    {
        // Arrange
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com",
            ShortCode = "abc",
            IsActive = false
        };

        _unitOfWork.ShortUrls.GetByShortCodeAsync("abc", Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.ResolveAsync("abc");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlDisabled);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task ResolveAsync_短網址已過期_應回傳錯誤()
    {
        // Arrange
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com",
            ShortCode = "abc",
            IsActive = true,
            ExpiresAt = _now.AddDays(-1) // 已過期
        };

        _unitOfWork.ShortUrls.GetByShortCodeAsync("abc", Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.ResolveAsync("abc");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlExpired);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task ResolveAsync_未過期_應成功()
    {
        // Arrange
        var shortUrl = new ShortUrl
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com",
            ShortCode = "abc",
            IsActive = true,
            ExpiresAt = _now.AddDays(1) // 未過期
        };

        _unitOfWork.ShortUrls.GetByShortCodeAsync("abc", Arg.Any<CancellationToken>())
            .Returns(shortUrl);

        // Act
        var result = await _sut.ResolveAsync("abc");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region GetStatsAsync 測試

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetStatsAsync_短網址存在_應回傳統計資料()
    {
        // Arrange
        var id = Guid.NewGuid();
        var shortUrl = new ShortUrl
        {
            Id = id,
            ShortCode = "abc",
            ClickCount = 100
        };

        var clicksByDate = new Dictionary<DateOnly, int>
        {
            { DateOnly.FromDateTime(_now.AddDays(-1)), 30 },
            { DateOnly.FromDateTime(_now), 70 }
        };

        var referrers = new Dictionary<string, int>
        {
            { "google.com", 50 },
            { "facebook.com", 30 }
        };

        var devices = new Dictionary<string, int>
        {
            { "mobile", 60 },
            { "desktop", 40 }
        };

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(shortUrl);
        _unitOfWork.UrlClicks.GetClicksByDateAsync(id, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(clicksByDate);
        _unitOfWork.UrlClicks.GetLastClickAtAsync(id, Arg.Any<CancellationToken>())
            .Returns(_now);
        _unitOfWork.UrlClicks.GetClicksByReferrerAsync(id, 10, Arg.Any<CancellationToken>())
            .Returns(referrers);
        _unitOfWork.UrlClicks.GetClicksByDeviceTypeAsync(id, Arg.Any<CancellationToken>())
            .Returns(devices);

        // Act
        var result = await _sut.GetStatsAsync(id, null, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalClicks.Should().Be(100);
        result.Value.LastClickAt.Should().Be(_now);
        result.Value.ClicksByDate.Should().HaveCount(2);
        result.Value.TopReferrers.Should().HaveCount(2);
        result.Value.DeviceBreakdown.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "ShortUrl")]
    public async Task GetStatsAsync_短網址不存在_應回傳錯誤()
    {
        // Arrange
        var id = Guid.NewGuid();

        _unitOfWork.ShortUrls.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ShortUrl?)null);

        // Act
        var result = await _sut.GetStatsAsync(id, null, null);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ShortUrlNotFound);
    }

    #endregion
}
