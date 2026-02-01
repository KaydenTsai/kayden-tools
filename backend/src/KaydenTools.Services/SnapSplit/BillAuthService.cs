using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;

namespace KaydenTools.Services.SnapSplit;

/// <summary>
/// 帳單授權服務 — 檢查使用者對帳單的存取權限（Scoped 生命週期，含 per-request 快取）
/// </summary>
public class BillAuthService : IBillAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly Dictionary<Guid, (bool isOwner, bool isParticipant)> _cache = new();

    public BillAuthService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsOwnerAsync(Guid billId, Guid userId, CancellationToken ct = default)
    {
        var (isOwner, _) = await ResolveAsync(billId, userId, ct);
        return isOwner;
    }

    public async Task<bool> IsParticipantAsync(Guid billId, Guid userId, CancellationToken ct = default)
    {
        var (_, isParticipant) = await ResolveAsync(billId, userId, ct);
        return isParticipant;
    }

    public async Task<bool> IsOwnerOrParticipantAsync(Guid billId, Guid userId, CancellationToken ct = default)
    {
        var (isOwner, isParticipant) = await ResolveAsync(billId, userId, ct);
        return isOwner || isParticipant;
    }

    public async Task<bool> IsOwnerOrParticipantByMemberIdAsync(Guid memberId, Guid userId, CancellationToken ct = default)
    {
        var member = await _unitOfWork.Members.GetByIdAsync(memberId, ct);
        if (member is null) return false;
        return await IsOwnerOrParticipantAsync(member.BillId, userId, ct);
    }

    public async Task<bool> IsOwnerOrParticipantByExpenseIdAsync(Guid expenseId, Guid userId, CancellationToken ct = default)
    {
        var expense = await _unitOfWork.Expenses.GetByIdAsync(expenseId, ct);
        if (expense is null) return false;
        return await IsOwnerOrParticipantAsync(expense.BillId, userId, ct);
    }

    private async Task<(bool isOwner, bool isParticipant)> ResolveAsync(Guid billId, Guid userId, CancellationToken ct)
    {
        if (_cache.TryGetValue(billId, out var cached))
            return cached;

        var bill = await _unitOfWork.Bills.GetByIdAsync(billId, ct);
        if (bill is null)
        {
            var result = (false, false);
            _cache[billId] = result;
            return result;
        }

        var isOwner = bill.OwnerId == userId;

        var members = await _unitOfWork.Members.GetByBillIdAsync(billId, ct);
        var isParticipant = members.Any(m => m.LinkedUserId == userId);

        var entry = (isOwner, isParticipant);
        _cache[billId] = entry;
        return entry;
    }
}
