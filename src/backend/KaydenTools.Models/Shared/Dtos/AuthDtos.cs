namespace KaydenTools.Models.Shared.Dtos;

/// <summary>
/// 身份驗證結果
/// </summary>
public record AuthResultDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

/// <summary>
/// 使用者資訊
/// </summary>
public record UserDto(
    Guid Id,
    string? Email,
    string? DisplayName,
    string? AvatarUrl
);

/// <summary>
/// 刷新令牌請求
/// </summary>
public record RefreshTokenRequestDto(
    string RefreshToken
);

/// <summary>
/// LINE 登入請求
/// </summary>
public record LineLoginRequestDto(
    string Code,
    string? State
);

/// <summary>
/// Google 登入請求
/// </summary>
public record GoogleLoginRequestDto(
    string Code
);

/// <summary>
/// LINE Token 回應
/// </summary>
public record LineTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? IdToken { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// LINE 使用者資料
/// </summary>
public record LineUserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Google Token 回應
/// </summary>
public record GoogleTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public string? IdToken { get; set; }
    public string? Scope { get; set; }
}

/// <summary>
/// Google 使用者資訊
/// </summary>
public record GoogleUserInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool VerifiedEmail { get; set; }
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Picture { get; set; }
    public string? Locale { get; set; }
}
