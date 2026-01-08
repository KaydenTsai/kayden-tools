using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Models.Shared.Entities;
using KaydenTools.Services.Auth;

namespace KaydenTools.Services.Tests.Auth;

/// <summary>
/// JwtService 單元測試
/// </summary>
public class JwtServiceTests
{
    private readonly JwtService _sut;
    private readonly JwtSettings _settings;

    public JwtServiceTests()
    {
        _settings = new JwtSettings
        {
            Secret = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly123456",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
        _sut = new JwtService(_settings);
    }

    #region GenerateAccessToken 測試

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateAccessToken_有效使用者_應產生有效JWT()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "測試使用者"
        };

        // Act
        var token = _sut.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be(_settings.Issuer);
        jwt.Audiences.Should().Contain(_settings.Audience);
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == user.DisplayName);
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateAccessToken_無Email使用者_不應包含Email聲明()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = null,
            DisplayName = "測試使用者"
        };

        // Act
        var token = _sut.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().NotContain(c => c.Type == JwtRegisteredClaimNames.Email);
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateAccessToken_無DisplayName使用者_不應包含Name聲明()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = null
        };

        // Act
        var token = _sut.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().NotContain(c => c.Type == "name");
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateAccessToken_應包含正確過期時間()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "測試使用者"
        };
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var token = _sut.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expectedExpiry = beforeGeneration.AddMinutes(_settings.AccessTokenExpirationMinutes);
        jwt.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateAccessToken_每次應產生不同JTI()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "測試使用者"
        };

        // Act
        var token1 = _sut.GenerateAccessToken(user);
        var token2 = _sut.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt1 = handler.ReadJwtToken(token1);
        var jwt2 = handler.ReadJwtToken(token2);

        var jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }

    #endregion

    #region GenerateRefreshToken 測試

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateRefreshToken_應產生非空字串()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateRefreshToken_應產生Base64編碼字串()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateRefreshToken_每次應產生不同令牌()
    {
        // Act
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => _sut.GenerateRefreshToken())
            .ToList();

        // Assert
        tokens.Distinct().Should().HaveCount(100);
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void GenerateRefreshToken_應有足夠長度()
    {
        // Act
        var token = _sut.GenerateRefreshToken();
        var bytes = Convert.FromBase64String(token);

        // Assert
        bytes.Length.Should().BeGreaterThanOrEqualTo(64);
    }

    #endregion

    #region ValidateToken 測試

    [Fact]
    [Trait("Category", "Auth")]
    public void ValidateToken_有效令牌_應回傳ClaimsPrincipal()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            DisplayName = "測試使用者"
        };
        var token = _sut.GenerateAccessToken(user);

        // Act
        var principal = _sut.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(user.Id.ToString());
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void ValidateToken_無效令牌_應回傳null()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var principal = _sut.ValidateToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void ValidateToken_篡改令牌_應回傳null()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "測試使用者"
        };
        var token = _sut.GenerateAccessToken(user);
        var tamperedToken = token.Substring(0, token.Length - 5) + "XXXXX";

        // Act
        var principal = _sut.ValidateToken(tamperedToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void ValidateToken_錯誤密鑰簽署的令牌_應回傳null()
    {
        // Arrange
        var otherSettings = new JwtSettings
        {
            Secret = "DifferentSecretKeyThatIsLongEnoughForTesting12345678",
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            AccessTokenExpirationMinutes = 15
        };
        var otherService = new JwtService(otherSettings);

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "測試使用者"
        };
        var tokenFromOtherService = otherService.GenerateAccessToken(user);

        // Act
        var principal = _sut.ValidateToken(tokenFromOtherService);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void ValidateToken_錯誤Issuer的令牌_應回傳null()
    {
        // Arrange
        var otherSettings = new JwtSettings
        {
            Secret = _settings.Secret,
            Issuer = "WrongIssuer",
            Audience = _settings.Audience,
            AccessTokenExpirationMinutes = 15
        };
        var otherService = new JwtService(otherSettings);

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "測試使用者"
        };
        var tokenFromOtherService = otherService.GenerateAccessToken(user);

        // Act
        var principal = _sut.ValidateToken(tokenFromOtherService);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Auth")]
    public void ValidateToken_空字串_應回傳null()
    {
        // Act
        var principal = _sut.ValidateToken(string.Empty);

        // Assert
        principal.Should().BeNull();
    }

    #endregion

    #region GetAccessTokenExpirationMinutes 測試

    [Fact]
    [Trait("Category", "Auth")]
    public void GetAccessTokenExpirationMinutes_應回傳設定值()
    {
        // Act
        var minutes = _sut.GetAccessTokenExpirationMinutes();

        // Assert
        minutes.Should().Be(_settings.AccessTokenExpirationMinutes);
    }

    #endregion

    #region GetRefreshTokenExpirationDays 測試

    [Fact]
    [Trait("Category", "Auth")]
    public void GetRefreshTokenExpirationDays_應回傳設定值()
    {
        // Act
        var days = _sut.GetRefreshTokenExpirationDays();

        // Assert
        days.Should().Be(_settings.RefreshTokenExpirationDays);
    }

    #endregion
}
