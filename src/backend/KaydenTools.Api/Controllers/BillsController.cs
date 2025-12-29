using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 帳單管理 API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BillsController : ControllerBase
{
    private readonly IBillService _billService;
    private readonly ICurrentUserService _currentUserService;

    public BillsController(IBillService billService, ICurrentUserService currentUserService)
    {
        _billService = billService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// 根據 ID 取得帳單
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>帳單詳情</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _billService.GetByIdAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<BillDto>.Ok(result.Value));
    }

    /// <summary>
    /// 根據分享碼取得帳單
    /// </summary>
    /// <param name="shareCode">分享碼</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>帳單詳情</returns>
    [HttpGet("share/{shareCode}")]
    [ProducesResponseType(typeof(ApiResponse<BillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByShareCode(string shareCode, CancellationToken ct)
    {
        var result = await _billService.GetByShareCodeAsync(shareCode, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<BillDto>.Ok(result.Value));
    }

    /// <summary>
    /// 取得當前使用者參與的帳單
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>帳單列表</returns>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BillDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyBills(CancellationToken ct)
    {

        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse.Fail(ErrorCodes.Unauthorized, "User not authenticated."));
        }

        var result = await _billService.GetByLinkedUserIdAsync(userId.Value, ct);
        return Ok(ApiResponse<IReadOnlyList<BillDto>>.Ok(result.Value));
    }

    /// <summary>
    /// 建立新帳單
    /// </summary>
    /// <param name="dto">帳單資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>新建立的帳單</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BillDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateBillDto dto, CancellationToken ct)
    {
        var result = await _billService.CreateAsync(dto, _currentUserService.UserId, ct);
        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id },
            ApiResponse<BillDto>.Ok(result.Value));
    }

    /// <summary>
    /// 更新帳單
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="dto">更新資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新後的帳單</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBillDto dto, CancellationToken ct)
    {
        var result = await _billService.UpdateAsync(id, dto, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<BillDto>.Ok(result.Value));
    }

    /// <summary>
    /// 刪除帳單
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _billService.DeleteAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return NoContent();
    }

    /// <summary>
    /// 產生分享碼
    /// </summary>
    /// <param name="id">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分享碼</returns>
    [HttpPost("{id:guid}/share-code")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateShareCode(Guid id, CancellationToken ct)
    {
        var result = await _billService.GenerateShareCodeAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse.Ok(new { shareCode = result.Value }));
    }

    /// <summary>
    /// 同步帳單（含成員、費用完整同步）
    /// </summary>
    /// <param name="dto">同步請求資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>同步結果（含 ID 映射）</returns>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse<SyncBillResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sync([FromBody] SyncBillRequestDto request, CancellationToken ct)
    {
        var result = await _billService.SyncBillAsync(request, _currentUserService.UserId, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.BillNotFound)
            {
                return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
            }

            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<SyncBillResponseDto>.Ok(result.Value));
    }
}
