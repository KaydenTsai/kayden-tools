using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 帳單成員管理 API
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class MembersController : ControllerBase
{
    private readonly IMemberService _memberService;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="memberService">成員服務</param>
    public MembersController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    /// <summary>
    /// 新增成員到帳單
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="dto">成員資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>新建立的成員</returns>
    [HttpPost("bills/{billId:guid}/members")]
    [ProducesResponseType(typeof(ApiResponse<MemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid billId, [FromBody] CreateMemberDto dto, CancellationToken ct)
    {
        var result = await _memberService.CreateAsync(billId, dto, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.BillNotFound)
            {
                return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
            }
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return CreatedAtAction(
            null,
            new { id = result.Value!.Id },
            ApiResponse<MemberDto>.Ok(result.Value));
    }

    /// <summary>
    /// 更新成員
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="dto">更新資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新後的成員</returns>
    [HttpPut("members/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMemberDto dto, CancellationToken ct)
    {
        var result = await _memberService.UpdateAsync(id, dto, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<MemberDto>.Ok(result.Value));
    }

    /// <summary>
    /// 刪除成員
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="ct">取消令牌</param>
    [HttpDelete("members/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _memberService.DeleteAsync(id, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.Conflict)
            {
                return Conflict(ApiResponse.Fail(result.Error.Code, result.Error.Message));
            }
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return NoContent();
    }
}