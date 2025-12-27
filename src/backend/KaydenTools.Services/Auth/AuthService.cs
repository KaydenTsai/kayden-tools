using System.Net.Http.Json;
using System.Text.Json;
using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Models.Shared.Dtos;
using KaydenTools.Models.Shared.Entities;
using KaydenTools.Models.Shared.Enums;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;

namespace KaydenTools.Services.Auth;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LineLoginSettings _lineSettings;
    private readonly GoogleLoginSettings _googleSettings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AuthService(
        IUnitOfWork unitOfWork,
        IJwtService jwtService,
        IDateTimeService dateTimeService,
        IHttpClientFactory httpClientFactory,
        LineLoginSettings lineSettings,
        GoogleLoginSettings googleSettings)
    {
        _unitOfWork = unitOfWork;
        _jwtService = jwtService;
        _dateTimeService = dateTimeService;
        _httpClientFactory = httpClientFactory;
        _lineSettings = lineSettings;
        _googleSettings = googleSettings;
    }

    public async Task<Result<AuthResultDto>> LoginWithLineAsync(string code, string? state, CancellationToken ct = default)
    {
        // Exchange code for token
        var tokenResponse = await ExchangeLineCodeAsync(code, ct);
        if (tokenResponse == null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.InvalidCredentials, "Failed to exchange LINE authorization code.");
        }

        // Get user profile
        var profile = await GetLineProfileAsync(tokenResponse.AccessToken, ct);
        if (profile == null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.InvalidCredentials, "Failed to get LINE user profile.");
        }

        // Find or create user
        var user = await _unitOfWork.Users.GetByLineUserIdAsync(profile.UserId, ct);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = profile.DisplayName,
                AvatarUrl = profile.PictureUrl,
                PrimaryProvider = AuthProvider.Line,
                LineUserId = profile.UserId,
                LinePictureUrl = profile.PictureUrl,
                CreatedAt = _dateTimeService.UtcNow
            };
            await _unitOfWork.Users.AddAsync(user, ct);
        }
        else
        {
            user.DisplayName = profile.DisplayName;
            user.LinePictureUrl = profile.PictureUrl;
            user.AvatarUrl ??= profile.PictureUrl;
            user.UpdatedAt = _dateTimeService.UtcNow;
            _unitOfWork.Users.Update(user);
        }

        var result = await CreateAuthResultAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success<AuthResultDto>(result);
    }

    public async Task<Result<AuthResultDto>> LoginWithGoogleAsync(string code, CancellationToken ct = default)
    {
        // Exchange code for token
        var tokenResponse = await ExchangeGoogleCodeAsync(code, ct);
        if (tokenResponse == null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.InvalidCredentials, "Failed to exchange Google authorization code.");
        }

        // Get user info
        var userInfo = await GetGoogleUserInfoAsync(tokenResponse.AccessToken, ct);
        if (userInfo == null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.InvalidCredentials, "Failed to get Google user info.");
        }

        // Find or create user
        var user = await _unitOfWork.Users.GetByGoogleUserIdAsync(userInfo.Id, ct);

        if (user == null)
        {
            // Check if email already exists
            if (!string.IsNullOrEmpty(userInfo.Email))
            {
                var existingUser = await _unitOfWork.Users.GetByEmailAsync(userInfo.Email, ct);
                if (existingUser != null)
                {
                    // Link Google account to existing user
                    existingUser.GoogleUserId = userInfo.Id;
                    existingUser.GooglePictureUrl = userInfo.Picture;
                    existingUser.UpdatedAt = _dateTimeService.UtcNow;
                    _unitOfWork.Users.Update(existingUser);
                    user = existingUser;
                }
            }

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = userInfo.Email,
                    DisplayName = userInfo.Name,
                    AvatarUrl = userInfo.Picture,
                    PrimaryProvider = AuthProvider.Google,
                    GoogleUserId = userInfo.Id,
                    GooglePictureUrl = userInfo.Picture,
                    CreatedAt = _dateTimeService.UtcNow
                };
                await _unitOfWork.Users.AddAsync(user, ct);
            }
        }
        else
        {
            user.DisplayName = userInfo.Name ?? user.DisplayName;
            user.GooglePictureUrl = userInfo.Picture;
            user.AvatarUrl ??= userInfo.Picture;
            user.UpdatedAt = _dateTimeService.UtcNow;
            _unitOfWork.Users.Update(user);
        }

        var result = await CreateAuthResultAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success<AuthResultDto>(result);
    }

    public async Task<Result<AuthResultDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken, ct);

        if (token == null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.InvalidToken, "Invalid refresh token.");
        }

        if (token.RevokedAt != null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.InvalidToken, "Refresh token has been revoked.");
        }

        if (token.ExpiresAt < _dateTimeService.UtcNow)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.TokenExpired, "Refresh token has expired.");
        }

        var user = await _unitOfWork.Users.GetByIdAsync(token.UserId, ct);
        if (user == null)
        {
            return Result.Failure<AuthResultDto>(ErrorCodes.NotFound, "User not found.");
        }

        // Revoke old token
        token.RevokedAt = _dateTimeService.UtcNow;
        _unitOfWork.RefreshTokens.Update(token);

        // Create new tokens
        var result = await CreateAuthResultAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success<AuthResultDto>(result);
    }

    public async Task<Result> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken, ct);

        if (token != null && token.RevokedAt == null)
        {
            token.RevokedAt = _dateTimeService.UtcNow;
            _unitOfWork.RefreshTokens.Update(token);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    public async Task<Result> RevokeAllTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _unitOfWork.RefreshTokens.GetActiveTokensByUserIdAsync(userId, ct);

        foreach (var token in tokens)
        {
            token.RevokedAt = _dateTimeService.UtcNow;
            _unitOfWork.RefreshTokens.Update(token);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<AuthResultDto> CreateAuthResultAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshTokenValue = _jwtService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = _dateTimeService.UtcNow.AddDays(_jwtService.GetRefreshTokenExpirationDays()),
            CreatedAt = _dateTimeService.UtcNow
        };

        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, ct);

        return new AuthResultDto(
            accessToken,
            refreshTokenValue,
            _dateTimeService.UtcNow.AddMinutes(_jwtService.GetAccessTokenExpirationMinutes()),
            new UserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl)
        );
    }

    private async Task<LineTokenResponse?> ExchangeLineCodeAsync(string code, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _lineSettings.CallbackUrl,
            ["client_id"] = _lineSettings.ChannelId,
            ["client_secret"] = _lineSettings.ChannelSecret
        });

        try
        {
            var response = await client.PostAsync("https://api.line.me/oauth2/v2.1/token", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LineTokenResponse>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<LineUserProfile?> GetLineProfileAsync(string accessToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await client.GetAsync("https://api.line.me/v2/profile", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<LineUserProfile>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GoogleTokenResponse?> ExchangeGoogleCodeAsync(string code, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _googleSettings.CallbackUrl,
            ["client_id"] = _googleSettings.ClientId,
            ["client_secret"] = _googleSettings.ClientSecret
        });

        try
        {
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GoogleUserInfo>(JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }
}
