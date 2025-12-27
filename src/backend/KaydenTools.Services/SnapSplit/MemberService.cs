using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;

namespace KaydenTools.Services.SnapSplit;

/// <summary>
/// 成員管理服務實作
/// </summary>
public class MemberService : IMemberService
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="unitOfWork">工作單元</param>
    public MemberService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// 建立成員
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="dto">成員資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>新建立的成員</returns>
    public async Task<Result<MemberDto>> CreateAsync(Guid billId, CreateMemberDto dto, CancellationToken ct = default)
    {
        // 檢查帳單是否存在
        var bill = await _unitOfWork.Bills.GetByIdAsync(billId, ct);
        if (bill == null)
        {
            return Result.Failure<MemberDto>(ErrorCodes.BillNotFound, "Bill not found.");
        }

        // 取得下一個顯示順序
        var displayOrder = await _unitOfWork.Members.GetNextDisplayOrderAsync(billId, ct);

        var member = new Member
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            Name = dto.Name,
            DisplayOrder = displayOrder
        };

        await _unitOfWork.Members.AddAsync(member, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new MemberDto(member.Id, member.Name, member.DisplayOrder, member.LinkedUserId);
    }

    /// <summary>
    /// 更新成員
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="dto">更新資料</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新後的成員</returns>
    public async Task<Result<MemberDto>> UpdateAsync(Guid id, UpdateMemberDto dto, CancellationToken ct = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(id, ct);
        if (member == null)
        {
            return Result.Failure<MemberDto>(ErrorCodes.MemberNotFound, "Member not found.");
        }

        member.Name = dto.Name;
        member.DisplayOrder = dto.DisplayOrder;

        _unitOfWork.Members.Update(member);
        await _unitOfWork.SaveChangesAsync(ct);

        return new MemberDto(member.Id, member.Name, member.DisplayOrder, member.LinkedUserId);
    }

    /// <summary>
    /// 刪除成員
    /// </summary>
    /// <param name="id">成員 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作結果</returns>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(id, ct);
        if (member == null)
        {
            return Result.Failure(ErrorCodes.MemberNotFound, "Member not found.");
        }

        // 檢查成員是否被費用參照
        var hasExpenses = await _unitOfWork.Expenses.AnyAsync(
            e => e.PaidById == id || e.Participants.Any(p => p.MemberId == id), ct);

        if (hasExpenses)
        {
            return Result.Failure(ErrorCodes.Conflict, "Cannot delete member that is referenced by expenses.");
        }

        _unitOfWork.Members.Remove(member);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}