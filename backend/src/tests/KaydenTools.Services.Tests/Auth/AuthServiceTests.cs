using FluentAssertions;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Models.Shared.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Auth;
using KaydenTools.Services.Interfaces;
using NSubstitute;

namespace KaydenTools.Services.Tests.Auth;

/// <summary>
/// AuthService 單元測試
/// 注意：OAuth 登入方法 (LoginWithLineAsync, LoginWithGoogleAsync) 因涉及外部 HTTP 呼叫，
/// 需要在整合測試中使用 mock HTTP handler 測試
/// </summary>
public class AuthServiceTests
{
    private readonly AuthService _sut;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DateTime _now;

    public AuthServiceTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _jwtService = Substitute.For<IJwtService>();
        _dateTimeService = Substitute.For<IDateTimeService>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        _now = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeService.UtcNow.Returns(_now);

        var lineSettings = new LineLoginSettings
        {
            ChannelId = "test-channel-id",
            ChannelSecret = "test-channel-secret",
            CallbackUrl = "https://example.com/callback"
        };

        var googleSettings = new GoogleLoginSettings
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            CallbackUrl = "https://example.com/callback"
        };

        _sut = new AuthService(
            _unitOfWork,
            _jwtService,
            _dateTimeService,
            _httpClientFactory,
            lineSettings,
            googleSettings
        );
    }

    #region GetUserByIdAsync 測試

    [Fact]
    [Trait("Category", "Auth")]
    public async Task GetUserByIdAsync_使用者存在_應回傳使用者()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            DisplayName = "測試使用者",
            Email = "test@example.com"
        };

        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var result = await _sut.GetUserByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.DisplayName.Should().Be("測試使用者");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task GetUserByIdAsync_使用者不存在_應回傳null()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _sut.GetUserByIdAsync(userId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region RefreshTokenAsync 測試

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RefreshTokenAsync_有效令牌_應回傳新令牌()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshTokenValue = "valid-refresh-token";

        var user = new User
        {
            Id = userId,
            DisplayName = "測試使用者",
            Email = "test@example.com"
        };

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = refreshTokenValue,
            ExpiresAt = _now.AddDays(7),
            CreatedAt = _now.AddDays(-1),
            RevokedAt = null
        };

        _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenValue, Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        _jwtService.GenerateAccessToken(user).Returns("new-access-token");
        _jwtService.GenerateRefreshToken().Returns("new-refresh-token");
        _jwtService.GetAccessTokenExpirationMinutes().Returns(15);
        _jwtService.GetRefreshTokenExpirationDays().Returns(7);

        // Act
        var result = await _sut.RefreshTokenAsync(refreshTokenValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access-token");
        result.Value.RefreshToken.Should().Be("new-refresh-token");
        result.Value.User.Id.Should().Be(userId);

        // 舊令牌應被撤銷
        refreshToken.RevokedAt.Should().Be(_now);
        _unitOfWork.RefreshTokens.Received(1).Update(refreshToken);

        // 應建立新令牌
        await _unitOfWork.RefreshTokens.Received(1).AddAsync(
            Arg.Is<RefreshToken>(t => t.Token == "new-refresh-token"),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RefreshTokenAsync_無效令牌_應回傳錯誤()
    {
        // Arrange
        var invalidToken = "invalid-refresh-token";

        _unitOfWork.RefreshTokens.GetByTokenAsync(invalidToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        // Act
        var result = await _sut.RefreshTokenAsync(invalidToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidToken);
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RefreshTokenAsync_已撤銷令牌_應回傳錯誤()
    {
        // Arrange
        var refreshTokenValue = "revoked-refresh-token";

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = refreshTokenValue,
            ExpiresAt = _now.AddDays(7),
            CreatedAt = _now.AddDays(-1),
            RevokedAt = _now.AddHours(-1) // 已被撤銷
        };

        _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenValue, Arg.Any<CancellationToken>())
            .Returns(refreshToken);

        // Act
        var result = await _sut.RefreshTokenAsync(refreshTokenValue);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.InvalidToken);
        result.Error.Message.Should().Contain("revoked");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RefreshTokenAsync_過期令牌_應回傳錯誤()
    {
        // Arrange
        var refreshTokenValue = "expired-refresh-token";

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = refreshTokenValue,
            ExpiresAt = _now.AddDays(-1), // 已過期
            CreatedAt = _now.AddDays(-8),
            RevokedAt = null
        };

        _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenValue, Arg.Any<CancellationToken>())
            .Returns(refreshToken);

        // Act
        var result = await _sut.RefreshTokenAsync(refreshTokenValue);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.TokenExpired);
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RefreshTokenAsync_使用者不存在_應回傳錯誤()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshTokenValue = "valid-refresh-token";

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = refreshTokenValue,
            ExpiresAt = _now.AddDays(7),
            CreatedAt = _now.AddDays(-1),
            RevokedAt = null
        };

        _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenValue, Arg.Any<CancellationToken>())
            .Returns(refreshToken);
        _unitOfWork.Users.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _sut.RefreshTokenAsync(refreshTokenValue);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.NotFound);
    }

    #endregion

    #region LogoutAsync 測試

    [Fact]
    [Trait("Category", "Auth")]
    public async Task LogoutAsync_有效令牌_應撤銷令牌()
    {
        // Arrange
        var refreshTokenValue = "valid-refresh-token";

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = refreshTokenValue,
            ExpiresAt = _now.AddDays(7),
            CreatedAt = _now.AddDays(-1),
            RevokedAt = null
        };

        _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenValue, Arg.Any<CancellationToken>())
            .Returns(refreshToken);

        // Act
        var result = await _sut.LogoutAsync(refreshTokenValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        refreshToken.RevokedAt.Should().Be(_now);
        _unitOfWork.RefreshTokens.Received(1).Update(refreshToken);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task LogoutAsync_無效令牌_仍應回傳成功()
    {
        // Arrange
        var invalidToken = "invalid-refresh-token";

        _unitOfWork.RefreshTokens.GetByTokenAsync(invalidToken, Arg.Any<CancellationToken>())
            .Returns((RefreshToken?)null);

        // Act
        var result = await _sut.LogoutAsync(invalidToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.RefreshTokens.DidNotReceive().Update(Arg.Any<RefreshToken>());
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task LogoutAsync_已撤銷令牌_不應重複撤銷()
    {
        // Arrange
        var refreshTokenValue = "already-revoked-token";
        var previousRevokedAt = _now.AddHours(-2);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = refreshTokenValue,
            ExpiresAt = _now.AddDays(7),
            CreatedAt = _now.AddDays(-1),
            RevokedAt = previousRevokedAt // 已被撤銷
        };

        _unitOfWork.RefreshTokens.GetByTokenAsync(refreshTokenValue, Arg.Any<CancellationToken>())
            .Returns(refreshToken);

        // Act
        var result = await _sut.LogoutAsync(refreshTokenValue);

        // Assert
        result.IsSuccess.Should().BeTrue();
        refreshToken.RevokedAt.Should().Be(previousRevokedAt); // 應保持原值
        _unitOfWork.RefreshTokens.DidNotReceive().Update(Arg.Any<RefreshToken>());
    }

    #endregion

    #region RevokeAllTokensAsync 測試

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RevokeAllTokensAsync_有多個活躍令牌_應全部撤銷()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var tokens = new List<RefreshToken>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = "token1",
                ExpiresAt = _now.AddDays(7),
                RevokedAt = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = "token2",
                ExpiresAt = _now.AddDays(5),
                RevokedAt = null
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = "token3",
                ExpiresAt = _now.AddDays(3),
                RevokedAt = null
            }
        };

        _unitOfWork.RefreshTokens.GetActiveTokensByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(tokens);

        // Act
        var result = await _sut.RevokeAllTokensAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        foreach (var token in tokens)
        {
            token.RevokedAt.Should().Be(_now);
            _unitOfWork.RefreshTokens.Received(1).Update(token);
        }

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Auth")]
    public async Task RevokeAllTokensAsync_無活躍令牌_應回傳成功()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _unitOfWork.RefreshTokens.GetActiveTokensByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<RefreshToken>());

        // Act
        var result = await _sut.RevokeAllTokensAsync(userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _unitOfWork.RefreshTokens.DidNotReceive().Update(Arg.Any<RefreshToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}
