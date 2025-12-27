using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
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

    public BillsController(IBillService billService)
    {
        _billService = billService;
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
        // TODO: Get owner ID from authenticated user
        Guid? ownerId = null;

        var result = await _billService.CreateAsync(dto, ownerId, ct);
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
}
