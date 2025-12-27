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
