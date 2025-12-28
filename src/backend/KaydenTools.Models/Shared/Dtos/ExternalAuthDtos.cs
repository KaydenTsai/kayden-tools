using System.Text.Json.Serialization;

namespace KaydenTools.Models.Shared.Dtos;

/// <summary>
/// LINE Token 回應
/// 註：LINE OAuth API 使用 snake_case
/// </summary>
public record LineTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

/// <summary>
/// LINE 使用者資料
/// 註：LINE Profile API 使用 camelCase
/// </summary>
public record LineUserProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("pictureUrl")]
    public string? PictureUrl { get; init; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; init; }
}

/// <summary>
/// Google Token 回應
/// 註：Google OAuth API 使用 snake_case
/// </summary>
public record GoogleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

/// <summary>
/// Google 使用者資訊
/// 註：Google UserInfo API 使用 snake_case
/// </summary>
public record GoogleUserInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("verified_email")]
    public bool VerifiedEmail { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("picture")]
    public string? Picture { get; init; }

    [JsonPropertyName("locale")]
    public string? Locale { get; init; }
}
