using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Mapster;

namespace KaydenTools.Services.SnapSplit;

public class BillService : IBillService
{
    private static readonly Random Random = new();
    private readonly IUnitOfWork _unitOfWork;

    public BillService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BillDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(id, ct);
        if (bill == null)
        {
            return Result.Failure<BillDto>(ErrorCodes.BillNotFound, "Bill not found.");
        }

        return MapToBillDto(bill);
    }

    public async Task<Result<BillDto>> GetByShareCodeAsync(string shareCode, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByShareCodeAsync(shareCode, ct);
        if (bill == null)
        {
            return Result.Failure<BillDto>(ErrorCodes.InvalidShareCode, "Bill not found.");
        }

        return MapToBillDto(bill);
    }

    public async Task<Result<IReadOnlyList<BillSummaryDto>>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        var bills = await _unitOfWork.Bills.GetByOwnerIdAsync(ownerId, ct);

        var summaries = bills.Select(b => new BillSummaryDto(
            b.Id,
            b.Name,
            b.Members.Count,
            b.Expenses.Count,
            b.Expenses.Sum(e => e.Amount * (1 + e.ServiceFeePercent / 100)),
            b.UpdatedAt ?? b.CreatedAt
        )).ToList();

        return summaries;
    }

    public async Task<Result<BillDto>> CreateAsync(CreateBillDto dto, Guid? ownerId, CancellationToken ct = default)
    {
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            OwnerId = ownerId
        };

        await _unitOfWork.Bills.AddAsync(bill, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(bill.Id, ct);
    }

    public async Task<Result<BillDto>> UpdateAsync(Guid id, UpdateBillDto dto, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(id, ct);
        if (bill == null)
        {
            return Result.Failure<BillDto>(ErrorCodes.BillNotFound, "Bill not found.");
        }

        bill.Name = dto.Name;
        _unitOfWork.Bills.Update(bill);
        await _unitOfWork.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(id, ct);
        if (bill == null)
        {
            return Result.Failure(ErrorCodes.BillNotFound, "Bill not found.");
        }

        _unitOfWork.Bills.Remove(bill);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<string>> GenerateShareCodeAsync(Guid id, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(id, ct);
        if (bill == null)
        {
            return Result.Failure<string>(ErrorCodes.BillNotFound, "Bill not found.");
        }

        if (!string.IsNullOrEmpty(bill.ShareCode))
        {
            return bill.ShareCode;
        }

        bill.ShareCode = GenerateShortCode();
        _unitOfWork.Bills.Update(bill);
        await _unitOfWork.SaveChangesAsync(ct);

        return bill.ShareCode;
    }

    public async Task<Result<SyncBillResponseDto>> SyncBillAsync(
        SyncBillRequestDto dto,
        Guid? ownerId,
        CancellationToken ct = default)
    {
        Bill bill;
        var memberIdMappings = new Dictionary<string, Guid>();
        var expenseIdMappings = new Dictionary<string, Guid>();
        var expenseItemIdMappings = new Dictionary<string, Guid>();

        // 1. 建立或取得帳單
        if (dto.RemoteId.HasValue)
        {
            var existing = await _unitOfWork.Bills.GetByIdWithDetailsAsync(dto.RemoteId.Value, ct);
            if (existing == null)
            {
                return Result.Failure<SyncBillResponseDto>(ErrorCodes.BillNotFound, "Bill not found.");
            }

            bill = existing;
            bill.Name = dto.Name;
        }
        else
        {
            bill = new Bill
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                OwnerId = ownerId,
                ShareCode = GenerateShortCode()
            };
            await _unitOfWork.Bills.AddAsync(bill, ct);
        }

        // 2. 同步成員
        var existingMemberIds = bill.Members.Select(m => m.Id).ToHashSet();
        var syncedMemberRemoteIds = new HashSet<Guid>();

        foreach (var memberDto in dto.Members)
        {
            Member member;
            if (memberDto.RemoteId.HasValue && existingMemberIds.Contains(memberDto.RemoteId.Value))
            {
                member = bill.Members.First(m => m.Id == memberDto.RemoteId.Value);
                member.Name = memberDto.Name;
                member.DisplayOrder = memberDto.DisplayOrder;
                syncedMemberRemoteIds.Add(member.Id);
            }
            else
            {
                member = new Member
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    Name = memberDto.Name,
                    DisplayOrder = memberDto.DisplayOrder
                };
                bill.Members.Add(member);
            }

            memberIdMappings[memberDto.LocalId] = member.Id;
        }

        // 移除不在同步清單中的成員
        var membersToRemove = bill.Members
            .Where(m => existingMemberIds.Contains(m.Id) && !syncedMemberRemoteIds.Contains(m.Id))
            .ToList();
        foreach (var member in membersToRemove)
        {
            bill.Members.Remove(member);
        }

        // 3. 同步費用
        var existingExpenseIds = bill.Expenses.Select(e => e.Id).ToHashSet();
        var syncedExpenseRemoteIds = new HashSet<Guid>();

        foreach (var expenseDto in dto.Expenses)
        {
            Expense expense;
            if (expenseDto.RemoteId.HasValue && existingExpenseIds.Contains(expenseDto.RemoteId.Value))
            {
                expense = bill.Expenses.First(e => e.Id == expenseDto.RemoteId.Value);
                UpdateExpenseFromDto(expense, expenseDto, memberIdMappings);
                syncedExpenseRemoteIds.Add(expense.Id);
            }
            else
            {
                expense = CreateExpenseFromDto(bill.Id, expenseDto, memberIdMappings);
                bill.Expenses.Add(expense);
            }

            expenseIdMappings[expenseDto.LocalId] = expense.Id;

            // 同步費用細項
            if (expenseDto.IsItemized && expenseDto.Items != null)
            {
                SyncExpenseItems(expense, expenseDto.Items, memberIdMappings, expenseItemIdMappings);
            }
        }

        // 移除不在同步清單中的費用
        var expensesToRemove = bill.Expenses
            .Where(e => existingExpenseIds.Contains(e.Id) && !syncedExpenseRemoteIds.Contains(e.Id))
            .ToList();
        foreach (var expense in expensesToRemove)
        {
            bill.Expenses.Remove(expense);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return new SyncBillResponseDto(
            bill.Id,
            bill.ShareCode,
            new SyncIdMappingsDto(memberIdMappings, expenseIdMappings, expenseItemIdMappings),
            DateTime.UtcNow
        );
    }

    private static Expense CreateExpenseFromDto(
        Guid billId,
        SyncExpenseDto dto,
        Dictionary<string, Guid> memberIdMappings)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            Name = dto.Name,
            Amount = dto.Amount,
            ServiceFeePercent = dto.ServiceFeePercent,
            IsItemized = dto.IsItemized,
            PaidById = dto.PaidByLocalId != null && memberIdMappings.TryGetValue(dto.PaidByLocalId, out var paidById)
                ? paidById
                : null
        };

        // 加入參與者
        foreach (var localId in dto.ParticipantLocalIds)
        {
            if (memberIdMappings.TryGetValue(localId, out var memberId))
            {
                expense.Participants.Add(new ExpenseParticipant
                {
                    ExpenseId = expense.Id,
                    MemberId = memberId
                });
            }
        }

        return expense;
    }

    private static void UpdateExpenseFromDto(
        Expense expense,
        SyncExpenseDto dto,
        Dictionary<string, Guid> memberIdMappings)
    {
        expense.Name = dto.Name;
        expense.Amount = dto.Amount;
        expense.ServiceFeePercent = dto.ServiceFeePercent;
        expense.IsItemized = dto.IsItemized;
        expense.PaidById = dto.PaidByLocalId != null && memberIdMappings.TryGetValue(dto.PaidByLocalId, out var paidById)
            ? paidById
            : null;

        // 更新參與者
        expense.Participants.Clear();
        foreach (var localId in dto.ParticipantLocalIds)
        {
            if (memberIdMappings.TryGetValue(localId, out var memberId))
            {
                expense.Participants.Add(new ExpenseParticipant
                {
                    ExpenseId = expense.Id,
                    MemberId = memberId
                });
            }
        }
    }

    private static void SyncExpenseItems(
        Expense expense,
        List<SyncExpenseItemDto> itemDtos,
        Dictionary<string, Guid> memberIdMappings,
        Dictionary<string, Guid> expenseItemIdMappings)
    {
        var existingItemIds = expense.Items.Select(i => i.Id).ToHashSet();
        var syncedItemRemoteIds = new HashSet<Guid>();

        foreach (var itemDto in itemDtos)
        {
            ExpenseItem item;
            if (itemDto.RemoteId.HasValue && existingItemIds.Contains(itemDto.RemoteId.Value))
            {
                item = expense.Items.First(i => i.Id == itemDto.RemoteId.Value);
                UpdateExpenseItemFromDto(item, itemDto, memberIdMappings);
                syncedItemRemoteIds.Add(item.Id);
            }
            else
            {
                item = CreateExpenseItemFromDto(expense.Id, itemDto, memberIdMappings);
                expense.Items.Add(item);
            }

            expenseItemIdMappings[itemDto.LocalId] = item.Id;
        }

        // 移除不在同步清單中的細項
        var itemsToRemove = expense.Items
            .Where(i => existingItemIds.Contains(i.Id) && !syncedItemRemoteIds.Contains(i.Id))
            .ToList();
        foreach (var item in itemsToRemove)
        {
            expense.Items.Remove(item);
        }
    }

    private static ExpenseItem CreateExpenseItemFromDto(
        Guid expenseId,
        SyncExpenseItemDto dto,
        Dictionary<string, Guid> memberIdMappings)
    {
        var item = new ExpenseItem
        {
            Id = Guid.NewGuid(),
            ExpenseId = expenseId,
            Name = dto.Name,
            Amount = dto.Amount,
            PaidById = memberIdMappings.TryGetValue(dto.PaidByLocalId, out var paidById)
                ? paidById
                : Guid.Empty
        };

        foreach (var localId in dto.ParticipantLocalIds)
        {
            if (memberIdMappings.TryGetValue(localId, out var memberId))
            {
                item.Participants.Add(new ExpenseItemParticipant
                {
                    ExpenseItemId = item.Id,
                    MemberId = memberId
                });
            }
        }

        return item;
    }

    private static void UpdateExpenseItemFromDto(
        ExpenseItem item,
        SyncExpenseItemDto dto,
        Dictionary<string, Guid> memberIdMappings)
    {
        item.Name = dto.Name;
        item.Amount = dto.Amount;
        item.PaidById = memberIdMappings.TryGetValue(dto.PaidByLocalId, out var paidById)
            ? paidById
            : item.PaidById;

        item.Participants.Clear();
        foreach (var localId in dto.ParticipantLocalIds)
        {
            if (memberIdMappings.TryGetValue(localId, out var memberId))
            {
                item.Participants.Add(new ExpenseItemParticipant
                {
                    ExpenseItemId = item.Id,
                    MemberId = memberId
                });
            }
        }
    }

    private static string GenerateShortCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[Random.Next(s.Length)]).ToArray());
    }

    private static BillDto MapToBillDto(Bill bill)
    {
        return new BillDto(
            bill.Id,
            bill.Name,
            bill.ShareCode,
            bill.CreatedAt,
            bill.UpdatedAt,
            bill.Members.Select(m => new MemberDto(m.Id, m.Name, m.DisplayOrder, m.LinkedUserId)).ToList(),
            bill.Expenses.Select(e => new ExpenseDto(
                e.Id,
                e.Name,
                e.Amount,
                e.ServiceFeePercent,
                e.IsItemized,
                e.PaidById,
                e.Participants.Select(p => p.MemberId).ToList(),
                e.IsItemized ? e.Items.Select(i => new ExpenseItemDto(
                    i.Id,
                    i.Name,
                    i.Amount,
                    i.PaidById,
                    i.Participants.Select(p => p.MemberId).ToList()
                )).ToList() : null,
                e.CreatedAt
            )).ToList()
        );
    }
}
