using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;

namespace KaydenTools.Services.SnapSplit;

/// <summary>
/// 費用管理服務實作
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="unitOfWork">工作單元</param>
    public ExpenseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 根據 ID 取得費用
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>費用詳情</returns>
    public async Task<Result<ExpenseDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _unitOfWork.Expenses.GetByIdWithDetailsAsync(id, ct);
        if (expense == null)
        {
            return Result.Failure<ExpenseDto>(ErrorCodes.ExpenseNotFound, "Expense not found.");
        }

        return MapToDto(expense);
    }

    /// <summary>
    /// 建立費用
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="dto">費用資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>新建立的費用</returns>
    public async Task<Result<ExpenseDto>> CreateAsync(Guid billId, CreateExpenseDto dto, CancellationToken ct = default)
    {
        // 檢查帳單是否存在
        var bill = await _unitOfWork.Bills.GetByIdAsync(billId, ct);
        if (bill == null)
        {
            return Result.Failure<ExpenseDto>(ErrorCodes.BillNotFound, "Bill not found.");
        }

        // 驗證付款者是否存在於帳單成員中
        if (dto.PaidById.HasValue)
        {
            var payer = await _unitOfWork.Members.GetByIdAsync(dto.PaidById.Value, ct);
            if (payer == null || payer.BillId != billId)
            {
                return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, "Payer not found in this bill.");
            }
        }

        // 驗證參與者是否都存在於帳單成員中
        var members = await _unitOfWork.Members.GetByBillIdAsync(billId, ct);
        var memberIds = members.Select(m => m.Id).ToHashSet();

        foreach (var participantId in dto.ParticipantIds)
        {
            if (!memberIds.Contains(participantId))
            {
                return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, $"Participant {participantId} not found in this bill.");
            }
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            Name = dto.Name,
            Amount = dto.Amount,
            ServiceFeePercent = dto.ServiceFeePercent,
            IsItemized = dto.IsItemized,
            PaidById = dto.PaidById
        };

        // 新增參與者
        foreach (var participantId in dto.ParticipantIds)
        {
            expense.Participants.Add(new ExpenseParticipant
            {
                ExpenseId = expense.Id,
                MemberId = participantId
            });
        }

        // 新增細項（如為細項模式）
        if (dto.IsItemized && dto.Items != null)
        {
            foreach (var itemDto in dto.Items)
            {
                // 驗證細項付款者
                if (!memberIds.Contains(itemDto.PaidById))
                {
                    return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, $"Item payer {itemDto.PaidById} not found in this bill.");
                }

                // 驗證細項參與者
                foreach (var itemParticipantId in itemDto.ParticipantIds)
                {
                    if (!memberIds.Contains(itemParticipantId))
                    {
                        return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, $"Item participant {itemParticipantId} not found in this bill.");
                    }
                }

                var item = new ExpenseItem
                {
                    Id = Guid.NewGuid(),
                    ExpenseId = expense.Id,
                    Name = itemDto.Name,
                    Amount = itemDto.Amount,
                    PaidById = itemDto.PaidById
                };

                foreach (var itemParticipantId in itemDto.ParticipantIds)
                {
                    item.Participants.Add(new ExpenseItemParticipant
                    {
                        ExpenseItemId = item.Id,
                        MemberId = itemParticipantId
                    });
                }

                expense.Items.Add(item);
            }
        }

        await _unitOfWork.Expenses.AddAsync(expense, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(expense);
    }

    /// <summary>
    /// 更新費用
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="dto">更新資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新後的費用</returns>
    public async Task<Result<ExpenseDto>> UpdateAsync(Guid id, UpdateExpenseDto dto, CancellationToken ct = default)
    {
        var expense = await _unitOfWork.Expenses.GetByIdWithDetailsAsync(id, ct);
        if (expense == null)
        {
            return Result.Failure<ExpenseDto>(ErrorCodes.ExpenseNotFound, "Expense not found.");
        }

        // 取得帳單成員
        var members = await _unitOfWork.Members.GetByBillIdAsync(expense.BillId, ct);
        var memberIds = members.Select(m => m.Id).ToHashSet();

        // 驗證付款者
        if (dto.PaidById.HasValue && !memberIds.Contains(dto.PaidById.Value))
        {
            return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, "Payer not found in this bill.");
        }

        // 驗證參與者
        foreach (var participantId in dto.ParticipantIds)
        {
            if (!memberIds.Contains(participantId))
            {
                return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, $"Participant {participantId} not found in this bill.");
            }
        }

        // 更新基本資料
        expense.Name = dto.Name;
        expense.Amount = dto.Amount;
        expense.ServiceFeePercent = dto.ServiceFeePercent;
        expense.PaidById = dto.PaidById;

        // 更新參與者（清除後重新新增）
        expense.Participants.Clear();
        foreach (var participantId in dto.ParticipantIds)
        {
            expense.Participants.Add(new ExpenseParticipant
            {
                ExpenseId = expense.Id,
                MemberId = participantId
            });
        }

        // 更新細項（如為細項模式）
        if (expense.IsItemized && dto.Items != null)
        {
            expense.Items.Clear();
            foreach (var itemDto in dto.Items)
            {
                // 驗證細項付款者
                if (!memberIds.Contains(itemDto.PaidById))
                {
                    return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, $"Item payer {itemDto.PaidById} not found in this bill.");
                }

                // 驗證細項參與者
                foreach (var itemParticipantId in itemDto.ParticipantIds)
                {
                    if (!memberIds.Contains(itemParticipantId))
                    {
                        return Result.Failure<ExpenseDto>(ErrorCodes.MemberNotFound, $"Item participant {itemParticipantId} not found in this bill.");
                    }
                }

                var item = new ExpenseItem
                {
                    Id = Guid.NewGuid(),
                    ExpenseId = expense.Id,
                    Name = itemDto.Name,
                    Amount = itemDto.Amount,
                    PaidById = itemDto.PaidById
                };

                foreach (var itemParticipantId in itemDto.ParticipantIds)
                {
                    item.Participants.Add(new ExpenseItemParticipant
                    {
                        ExpenseItemId = item.Id,
                        MemberId = itemParticipantId
                    });
                }

                expense.Items.Add(item);
            }
        }

        _unitOfWork.Expenses.Update(expense);
        await _unitOfWork.SaveChangesAsync(ct);

        // 重新取得完整資料
        return await GetByIdAsync(id, ct);
    }

    /// <summary>
    /// 刪除費用
    /// </summary>
    /// <param name="id">費用 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作結果</returns>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _unitOfWork.Expenses.GetByIdAsync(id, ct);
        if (expense == null)
        {
            return Result.Failure(ErrorCodes.ExpenseNotFound, "Expense not found.");
        }

        _unitOfWork.Expenses.Remove(expense);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static ExpenseDto MapToDto(Expense expense)
    {
        return new ExpenseDto(
            expense.Id,
            expense.Name,
            expense.Amount,
            expense.ServiceFeePercent,
            expense.IsItemized,
            expense.PaidById,
            expense.Participants.Select(p => p.MemberId).ToList(),
            expense.IsItemized ? expense.Items.Select(i => new ExpenseItemDto(
                i.Id,
                i.Name,
                i.Amount,
                i.PaidById,
                i.Participants.Select(p => p.MemberId).ToList()
            )).ToList() : null,
            expense.CreatedAt
        );
    }
}