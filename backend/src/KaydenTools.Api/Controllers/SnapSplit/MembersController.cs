using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers.SnapSplit;

/// <summary>
/// 帳單成員管理 API
/// </summary>
[ApiController]
[Route("api/snap-split")]
[ApiExplorerSettings(GroupName = "snapsplit")]
[Tags("Members")]
[Produces("application/json")]
public class MembersController : ControllerBase
{
    private readonly IBillAuthService _billAuthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMemberService _memberService;

    /// <summary>
    /// 建構子
    /// </summary>
    public MembersController(IMemberService memberService, ICurrentUserService currentUserService, IBillAuthService billAuthService)
    {
        _memberService = memberService;
        _currentUserService = currentUserService;
        _billAuthService = billAuthService;
    }

    /// <summary>
    /// 新增成員到帳單
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="dto">成員資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>新建立的成員</returns>
    [HttpPost("bills/{billId:guid}/members")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MemberDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid billId, [FromBody] CreateMemberDto dto, CancellationToken ct)
    {
        if (!await _billAuthService.IsOwnerOrParticipantAsync(billId, _currentUserService.UserId!.Value, ct))
            return StatusCode(403, ApiResponse.Fail(ErrorCodes.Forbidden, "You do not have permission."));

        var result = await _memberService.CreateAsync(billId, dto, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.BillNotFound)
                return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
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
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMemberDto dto, CancellationToken ct)
    {
        if (!await _billAuthService.IsOwnerOrParticipantByMemberIdAsync(id, _currentUserService.UserId!.Value, ct))
            return StatusCode(403, ApiResponse.Fail(ErrorCodes.Forbidden, "You do not have permission."));

        var result = await _memberService.UpdateAsync(id, dto, ct);
        if (result.IsFailure) return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));

        return Ok(ApiResponse<MemberDto>.Ok(result.Value));
    }

    /// <summary>
    /// 刪除成員
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="ct">取消令牌</param>
    [HttpDelete("members/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await _billAuthService.IsOwnerOrParticipantByMemberIdAsync(id, _currentUserService.UserId!.Value, ct))
            return StatusCode(403, ApiResponse.Fail(ErrorCodes.Forbidden, "You do not have permission."));

        var result = await _memberService.DeleteAsync(id, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.Conflict)
                return Conflict(ApiResponse.Fail(result.Error.Code, result.Error.Message));
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return NoContent();
    }

    /// <summary>
    /// 認領成員（綁定當前使用者）
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="dto">認領資訊</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>認領結果</returns>
    [HttpPost("members/{id:guid}/claim")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ClaimMemberResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Claim(Guid id, [FromBody] ClaimMemberDto dto, CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized(ApiResponse.Fail(ErrorCodes.Unauthorized, "User not authenticated."));

        var result = await _memberService.ClaimAsync(id, userId.Value, dto, ct);
        if (result.IsFailure)
            return result.Error.Code switch
            {
                ErrorCodes.MemberNotFound => NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message)),
                ErrorCodes.MemberAlreadyClaimed or ErrorCodes.UserAlreadyClaimedOther =>
                    Conflict(ApiResponse.Fail(result.Error.Code, result.Error.Message)),
                _ => BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message))
            };

        return Ok(ApiResponse<ClaimMemberResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// 取消認領成員
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="ct">取消令牌</param>
    [HttpDelete("members/{id:guid}/claim")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unclaim(Guid id, CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue) return Unauthorized(ApiResponse.Fail(ErrorCodes.Unauthorized, "User not authenticated."));

        var result = await _memberService.UnclaimAsync(id, userId.Value, ct);
        if (result.IsFailure)
            return result.Error.Code switch
            {
                ErrorCodes.MemberNotFound => NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message)),
                ErrorCodes.UnauthorizedUnclaim => Forbid(),
                _ => BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message))
            };

        return NoContent();
    }
}
