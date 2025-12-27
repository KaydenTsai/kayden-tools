using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace KaydenTools.Api.Controllers;

/// <summary>
/// 費用管理 API
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="expenseService">費用服務</param>
    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// 根據 ID 取得費用
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>費用詳情</returns>
    [HttpGet("expenses/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _expenseService.GetByIdAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<ExpenseDto>.Ok(result.Value));
    }

    /// <summary>
    /// 新增費用到帳單
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="dto">費用資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>新建立的費用</returns>
    [HttpPost("bills/{billId:guid}/expenses")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(Guid billId, [FromBody] CreateExpenseDto dto, CancellationToken ct)
    {
        var result = await _expenseService.CreateAsync(billId, dto, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.BillNotFound)
            {
                return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
            }
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id },
            ApiResponse<ExpenseDto>.Ok(result.Value));
    }

    /// <summary>
    /// 更新費用
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="dto">更新資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新後的費用</returns>
    [HttpPut("expenses/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseDto dto, CancellationToken ct)
    {
        var result = await _expenseService.UpdateAsync(id, dto, ct);
        if (result.IsFailure)
        {
            if (result.Error.Code == ErrorCodes.ExpenseNotFound)
            {
                return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
            }
            return BadRequest(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return Ok(ApiResponse<ExpenseDto>.Ok(result.Value));
    }

    /// <summary>
    /// 刪除費用
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="ct">取消令牌</param>
    [HttpDelete("expenses/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _expenseService.DeleteAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(ApiResponse.Fail(result.Error.Code, result.Error.Message));
        }

        return NoContent();
    }
}