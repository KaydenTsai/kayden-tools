using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.UrlShortener.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 短網址管理 API
/// </summary>
[ApiController]
[Route("api/urls")]
[Produces("application/json")]
public class ShortUrlsController : ControllerBase
{
    private readonly IShortUrlService _shortUrlService;
    private readonly ICurrentUserService _currentUserService;

    public ShortUrlsController(
        IShortUrlService shortUrlService,
        ICurrentUserService currentUserService)
    {
        _shortUrlService = shortUrlService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// 建立短網址
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ShortUrlDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateShortUrlDto dto, CancellationToken ct)
    {
        var result = await _shortUrlService.CreateAsync(dto, _currentUserService.UserId, ct);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id },
            ApiResponse<ShortUrlDto>.Ok(result.Value));
    }

    /// <summary>
    /// 根據 ID 取得短網址
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ShortUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _shortUrlService.GetByIdAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<ShortUrlDto>.Ok(result.Value));
    }

    /// <summary>
    /// 取得目前使用者的所有短網址（需登入）
    /// </summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ShortUrlSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyUrls(CancellationToken ct)
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail(ErrorCodes.Unauthorized, "Authentication required."));
        }

        var result = await _shortUrlService.GetByOwnerIdAsync(_currentUserService.UserId.Value, ct);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<IReadOnlyList<ShortUrlSummaryDto>>.Ok(result.Value));
    }

    /// <summary>
    /// 更新短網址
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ShortUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShortUrlDto dto, CancellationToken ct)
    {
        var result = await _shortUrlService.UpdateAsync(id, dto, _currentUserService.UserId, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<ShortUrlDto>.Ok(result.Value));
    }

    /// <summary>
    /// 刪除短網址
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _shortUrlService.DeleteAsync(id, _currentUserService.UserId, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return NoContent();
    }

    /// <summary>
    /// 取得短網址統計
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    [ProducesResponseType(typeof(ApiResponse<UrlStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(
        Guid id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var result = await _shortUrlService.GetStatsAsync(id, from, to, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<UrlStatsDto>.Ok(result.Value));
    }
}