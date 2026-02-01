using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers.SnapSplit;

/// <summary>
/// 結算管理 API
/// </summary>
[ApiController]
[Route("api/snap-split/bills/{billId:guid}/settlement")]
[Authorize]
[ApiExplorerSettings(GroupName = "snapsplit")]
[Tags("Settlements")]
[Produces("application/json")]
public class SettlementsController : ControllerBase
{
    private readonly IBillAuthService _billAuthService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISettlementService _settlementService;

    /// <summary>
    /// 建構子
    /// </summary>
    public SettlementsController(ISettlementService settlementService, IBillAuthService billAuthService, ICurrentUserService currentUserService)
    {
        _settlementService = settlementService;
        _billAuthService = billAuthService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// 計算帳單結算
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>結算結果</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<SettlementResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Calculate(Guid billId, CancellationToken ct)
    {
        if (!await _billAuthService.IsOwnerOrParticipantAsync(billId, _currentUserService.UserId!.Value, ct))
            return StatusCode(403, ApiResponse.Fail(ErrorCodes.Forbidden, "You do not have permission."));

        var result = await _settlementService.CalculateAsync(billId, ct);
        if (result.IsFailure) return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));

        return Ok(ApiResponse<SettlementResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// 切換轉帳結清狀態
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="request">轉帳資訊</param>
    /// <param name="ct">取消令牌</param>
    [HttpPost("toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleSettled(
        Guid billId,
        [FromBody] ToggleSettledRequest request,
        CancellationToken ct)
    {
        if (!await _billAuthService.IsOwnerOrParticipantAsync(billId, _currentUserService.UserId!.Value, ct))
            return StatusCode(403, ApiResponse.Fail(ErrorCodes.Forbidden, "You do not have permission."));

        var result = await _settlementService.ToggleSettledAsync
        (
            billId,
            request.FromMemberId,
            request.ToMemberId,
            ct
        );

        if (result.IsFailure) return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));

        return NoContent();
    }
}

/// <summary>
/// 切換結清狀態請求
/// </summary>
public record ToggleSettledRequest(Guid FromMemberId, Guid ToMemberId);
