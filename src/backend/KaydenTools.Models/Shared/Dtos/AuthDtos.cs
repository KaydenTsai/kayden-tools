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
