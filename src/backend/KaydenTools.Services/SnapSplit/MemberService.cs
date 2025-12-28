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
    public async Task<Result<MemberDto>> CreateAsync(Guid billId, CreateMemberDto dto, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(billId, ct);
        if (bill == null)
        {
            return Result.Failure<MemberDto>(ErrorCodes.BillNotFound, "Bill not found.");
        }

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

        return ToMemberDto(member);
    }

    /// <summary>
    /// 更新成員
    /// </summary>
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

        return ToMemberDto(member);
    }

    /// <summary>
    /// 刪除成員
    /// </summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(id, ct);
        if (member == null)
        {
            return Result.Failure(ErrorCodes.MemberNotFound, "Member not found.");
        }

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

    /// <summary>
    /// 認領成員
    /// </summary>
    public async Task<Result<ClaimMemberResultDto>> ClaimAsync(Guid memberId, Guid userId, ClaimMemberDto dto, CancellationToken ct = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(memberId, ct);
        if (member == null)
        {
            return Result.Failure<ClaimMemberResultDto>(ErrorCodes.MemberNotFound, "Member not found.");
        }

        // 檢查成員是否已被認領
        if (member.LinkedUserId.HasValue)
        {
            return Result.Failure<ClaimMemberResultDto>(ErrorCodes.MemberAlreadyClaimed, "Member is already claimed by another user.");
        }

        // 檢查使用者是否已在此帳單認領其他成員
        var billMembers = await _unitOfWork.Members.GetByBillIdAsync(member.BillId, ct);
        var alreadyClaimed = billMembers.FirstOrDefault(m => m.LinkedUserId == userId);
        if (alreadyClaimed != null)
        {
            return Result.Failure<ClaimMemberResultDto>(ErrorCodes.UserAlreadyClaimedOther,
                $"You have already claimed member '{alreadyClaimed.Name}' in this bill.");
        }

        // 取得使用者資訊
        var user = await _unitOfWork.Users.GetByIdAsync(userId, ct);
        if (user == null)
        {
            return Result.Failure<ClaimMemberResultDto>(ErrorCodes.NotFound, "User not found.");
        }

        // 執行認領
        member.OriginalName ??= member.Name; // 只在首次認領時保存原始名稱
        member.Name = dto.DisplayName ?? user.DisplayName ?? member.Name;
        member.LinkedUserId = userId;
        member.ClaimedAt = DateTime.UtcNow;

        _unitOfWork.Members.Update(member);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ClaimMemberResultDto(
            member.Id,
            member.Name,
            member.OriginalName,
            userId,
            user.DisplayName,
            user.AvatarUrl,
            member.ClaimedAt.Value
        );
    }

    /// <summary>
    /// 取消認領成員
    /// </summary>
    public async Task<Result> UnclaimAsync(Guid memberId, Guid userId, CancellationToken ct = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(memberId, ct);
        if (member == null)
        {
            return Result.Failure(ErrorCodes.MemberNotFound, "Member not found.");
        }

        // 檢查成員是否已被認領
        if (!member.LinkedUserId.HasValue)
        {
            return Result.Failure(ErrorCodes.MemberNotClaimed, "Member is not claimed.");
        }

        // 檢查權限：只有認領者本人或帳單擁有者可以取消
        var bill = await _unitOfWork.Bills.GetByIdAsync(member.BillId, ct);
        var isOwner = bill?.OwnerId == userId;
        var isClaimer = member.LinkedUserId == userId;

        if (!isOwner && !isClaimer)
        {
            return Result.Failure(ErrorCodes.UnauthorizedUnclaim, "Only the claimer or bill owner can unclaim this member.");
        }

        // 執行取消認領
        member.Name = member.OriginalName ?? member.Name; // 還原原始名稱
        member.OriginalName = null;
        member.LinkedUserId = null;
        member.ClaimedAt = null;

        _unitOfWork.Members.Update(member);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// 轉換為 MemberDto
    /// </summary>
    private static MemberDto ToMemberDto(Member member)
    {
        return new MemberDto(
            member.Id,
            member.Name,
            member.OriginalName,
            member.DisplayOrder,
            member.LinkedUserId,
            member.LinkedUser?.DisplayName,
            member.LinkedUser?.AvatarUrl,
            member.ClaimedAt
        );
    }
}