using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Configuration.Settings;
using KaydenTools.Models.Shared.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 身份驗證 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly LineLoginSettings _lineSettings;
    private readonly GoogleLoginSettings _googleSettings;

    public AuthController(
        IAuthService authService,
        LineLoginSettings lineSettings,
        GoogleLoginSettings googleSettings)
    {
        _authService = authService;
        _lineSettings = lineSettings;
        _googleSettings = googleSettings;
    }

    /// <summary>
    /// 取得可用的登入方式
    /// </summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult GetAvailableProviders()
    {
        return Ok(ApiResponse.Ok(new
        {
            line = _lineSettings.IsConfigured,
            google = _googleSettings.IsConfigured
        }));
    }

    /// <summary>
    /// 取得 LINE 登入 URL
    /// </summary>
    /// <param name="redirectUri">登入成功後的重導向 URL</param>
    /// <returns>LINE 授權 URL</returns>
    [HttpGet("line/url")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public IActionResult GetLineLoginUrl([FromQuery] string? redirectUri)
    {
        if (!_lineSettings.IsConfigured)
        {
            return BadRequest(ApiResponse.Fail(ErrorCodes.BadRequest, "LINE login is not configured."));
        }

        var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var url = $"https://access.line.me/oauth2/v2.1/authorize?" +
                  $"response_type=code&" +
                  $"client_id={_lineSettings.ChannelId}&" +
                  $"redirect_uri={Uri.EscapeDataString(_lineSettings.CallbackUrl)}&" +
                  $"state={state}&" +
                  $"scope=profile%20openid";

        return Ok(ApiResponse.Ok(new { url, state }));
    }

    /// <summary>
    /// LINE 登入回呼
    /// </summary>
    [HttpPost("line/callback")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LineCallback([FromBody] LineLoginRequestDto request, CancellationToken ct)
    {
        if (!_lineSettings.IsConfigured)
        {
            return BadRequest(ApiResponse.Fail(ErrorCodes.BadRequest, "LINE login is not configured."));
        }

        var result = await _authService.LoginWithLineAsync(request.Code, request.State, ct);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<AuthResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// 取得 Google 登入 URL
    /// </summary>
    /// <param name="redirectUri">登入成功後的重導向 URL</param>
    /// <returns>Google 授權 URL</returns>
    [HttpGet("google/url")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public IActionResult GetGoogleLoginUrl([FromQuery] string? redirectUri)
    {
        if (!_googleSettings.IsConfigured)
        {
            return BadRequest(ApiResponse.Fail(ErrorCodes.BadRequest, "Google login is not configured."));
        }

        var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var url = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                  $"response_type=code&" +
                  $"client_id={_googleSettings.ClientId}&" +
                  $"redirect_uri={Uri.EscapeDataString(_googleSettings.CallbackUrl)}&" +
                  $"state={state}&" +
                  $"scope=openid%20email%20profile";

        return Ok(ApiResponse.Ok(new { url, state }));
    }

    /// <summary>
    /// Google 登入回呼
    /// </summary>
    [HttpPost("google/callback")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleCallback([FromBody] GoogleLoginRequestDto request, CancellationToken ct)
    {
        if (!_googleSettings.IsConfigured)
        {
            return BadRequest(ApiResponse.Fail(ErrorCodes.BadRequest, "Google login is not configured."));
        }

        var result = await _authService.LoginWithGoogleAsync(request.Code, ct);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<AuthResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// 刷新 Token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken, ct);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<AuthResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// 登出
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        await _authService.LogoutAsync(request.RefreshToken, ct);
        return Ok(ApiResponse.Ok());
    }

    /// <summary>
    /// 取得目前使用者資訊
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst("sub")?.Value;
        var email = User.FindFirst("email")?.Value;
        var name = User.FindFirst("name")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse.Fail(ErrorCodes.Unauthorized, "User not authenticated."));
        }

        return Ok(ApiResponse.Ok(new
        {
            id = Guid.Parse(userId),
            email,
            displayName = name
        }));
    }
}
