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

    public async Task<Result<IReadOnlyList<BillSummaryDto>>> GetByOwnerIdAsync(Guid ownerId,
        CancellationToken ct = default)
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

    public async Task<Result<IReadOnlyList<BillDto>>> GetByLinkedUserIdAsync(Guid userId,
        CancellationToken ct = default)
    {
        var bills = await _unitOfWork.Bills.GetByLinkedUserIdAsync(userId, ct);

        return bills.Select(MapToBillDto).ToList();
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

    /// <summary>
    /// 同步帳單資料（支援增量更新、樂觀鎖與明確刪除）
    /// </summary>
    /// <param name="dto">同步請求資料</param>
    /// <param name="ownerId">當前使用者 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>同步結果</returns>
    public async Task<Result<SyncBillResponseDto>> SyncBillAsync(
        SyncBillRequestDto dto,
        Guid? ownerId,
        CancellationToken ct = default)
    {
        Bill bill;
        var memberIdMappings = new Dictionary<string, Guid>();
        var expenseIdMappings = new Dictionary<string, Guid>();
        var expenseItemIdMappings = new Dictionary<string, Guid>();

        // 1. 建立或取得帳單並檢查版本
        if (dto.RemoteId.HasValue)
        {
            var existing = await _unitOfWork.Bills.GetByIdWithDetailsAsync(dto.RemoteId.Value, ct);
            if (existing == null)
            {
                return Result.Failure<SyncBillResponseDto>(ErrorCodes.BillNotFound, "Bill not found.");
            }

            // 樂觀鎖檢查：如果前端基底版本不等於資料庫版本，則發生衝突
            if (dto.BaseVersion != existing.Version)
            {
                return Result.Success(new SyncBillResponseDto(
                    existing.Id,
                    existing.Version,
                    existing.ShareCode,
                    new SyncIdMappingsDto(new(), new(), new()),
                    DateTime.UtcNow,
                    MapToBillDto(existing) // 回傳最新資料供前端合併
                ));
            }

            bill = existing;
            if (!string.IsNullOrEmpty(dto.Name))
            {
                bill.Name = dto.Name;
            }

            // 核心補強：先載入資料庫中現有的所有成員到 Mapping 表中
            // 這樣即便前端這次沒傳送該成員的更新，費用的關聯 (PaidBy, Participants) 也能找得到人
            foreach (var m in bill.Members)
            {
                // 我們需要知道這個成員對應前端的 LocalId
                // 但後端其實不知道 LocalId。這是一個問題。
                // 方案：前端在同步費用時，傳送的 PaidByLocalId 若已經是 GUID 格式，
                // 代表它已經是 RemoteId。
            }
        }
        else
        {
            // 首次上傳
            bill = new Bill
            {
                Id = Guid.NewGuid(),
                Name = dto.Name ?? "New Bill",
                OwnerId = ownerId,
                ShareCode = GenerateShortCode(),
                Version = 1
            };
            await _unitOfWork.Bills.AddAsync(bill, ct);
        }

        // 2. 同步成員 (Upsert)
        // 為了讓後面的費用能找到關聯，我們先跑一遍成員同步，建立完整的 LocalId -> RemoteId 映射
        foreach (var memberDto in dto.Members.Upsert ?? Enumerable.Empty<SyncMemberDto>())
        {
            Member member;
            if (memberDto.RemoteId.HasValue)
            {
                member = bill.Members.FirstOrDefault(m => m.Id == memberDto.RemoteId.Value);
                if (member != null)
                {
                    member.Name = memberDto.Name;
                    member.DisplayOrder = memberDto.DisplayOrder;
                    member.LinkedUserId = memberDto.LinkedUserId;
                    member.ClaimedAt = memberDto.ClaimedAt;
                }
                else
                {
                    // 若有 RemoteId 但找不到，視為重建
                    member = new Member
                    {
                        Id = memberDto.RemoteId.Value,
                        BillId = bill.Id,
                        Name = memberDto.Name,
                        DisplayOrder = memberDto.DisplayOrder,
                        LinkedUserId = memberDto.LinkedUserId,
                        ClaimedAt = memberDto.ClaimedAt
                    };
                    bill.Members.Add(member);
                }
            }
            else
            {
                member = new Member
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    Name = memberDto.Name,
                    DisplayOrder = memberDto.DisplayOrder,
                    LinkedUserId = memberDto.LinkedUserId,
                    ClaimedAt = memberDto.ClaimedAt
                };
                bill.Members.Add(member);
            }

            memberIdMappings[memberDto.LocalId] = member.Id;
        }

        // 注意：除了 upsert 的成員，資料庫裡原本就有但沒被改動的成員，也應該放進 Mapping
        // 關鍵：前端在建構請求時，必須把「所有成員」都放在 upsert 裡面，
        // 這樣我們才能建立 LocalId 到 RemoteId 的完整映射。
        // 目前前端的 billAdapter.ts 確實是送出所有成員，所以這裡沒問題。

        // 3. 處理成員刪除 (Explicit Delete)
        foreach (var deletedIdStr in dto.Members.DeletedIds ?? Enumerable.Empty<string>())
        {
            if (Guid.TryParse(deletedIdStr, out var deletedId))
            {
                var member = bill.Members.FirstOrDefault(m => m.Id == deletedId);
                if (member != null)
                {
                    bill.Members.Remove(member);
                }
            }
        }

        // 4. 同步費用 (Upsert)
        foreach (var expenseDto in dto.Expenses.Upsert ?? Enumerable.Empty<SyncExpenseDto>())
        {
            Expense expense;
            if (expenseDto.RemoteId.HasValue)
            {
                expense = bill.Expenses.FirstOrDefault(e => e.Id == expenseDto.RemoteId.Value);
                if (expense != null)
                {
                    UpdateExpenseFromDto(expense, expenseDto, memberIdMappings);
                }
                else
                {
                    expense = CreateExpenseFromDto(bill.Id, expenseDto, memberIdMappings);
                    bill.Expenses.Add(expense);
                }
            }
            else
            {
                expense = CreateExpenseFromDto(bill.Id, expenseDto, memberIdMappings);
                bill.Expenses.Add(expense);
            }

            expenseIdMappings[expenseDto.LocalId] = expense.Id;

            // 同步費用細項
            if (expenseDto.Items != null)
            {
                SyncExpenseItemsIncremental(expense, expenseDto.Items, memberIdMappings, expenseItemIdMappings);
            }
        }

        // 5. 處理費用刪除 (Explicit Delete)
        foreach (var deletedIdStr in dto.Expenses.DeletedIds ?? Enumerable.Empty<string>())
        {
            if (Guid.TryParse(deletedIdStr, out var deletedId))
            {
                var expense = bill.Expenses.FirstOrDefault(e => e.Id == deletedId);
                if (expense != null)
                {
                    bill.Expenses.Remove(expense);
                }
            }
        }

        // 更新帳單版本號與時間
        bill.Version++;
        bill.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            // 並發衝突（例如同一時間有另一個請求修改了資料）
            // 重新讀取最新資料並回傳，讓前端重試
            _unitOfWork.ClearChangeTracker(); // 清除追蹤，避免污染
            var latest = await _unitOfWork.Bills.GetByIdWithDetailsAsync(bill.Id, ct);
            if (latest == null)
            {
                return Result.Failure<SyncBillResponseDto>(ErrorCodes.BillNotFound, "Bill deleted during sync.");
            }

            return Result.Success(new SyncBillResponseDto(
                latest.Id,
                latest.Version,
                latest.ShareCode,
                new SyncIdMappingsDto(new(), new(), new()),
                DateTime.UtcNow,
                MapToBillDto(latest)
            ));
        }

        return new SyncBillResponseDto(
            bill.Id,
            bill.Version,
            bill.ShareCode,
            new SyncIdMappingsDto(memberIdMappings, expenseIdMappings, expenseItemIdMappings),
            DateTime.UtcNow
        );
    }

        private static void SyncExpenseItemsIncremental(

            Expense expense,

            SyncExpenseItemCollectionDto collection,

            Dictionary<string, Guid> memberIdMappings,

            Dictionary<string, Guid> expenseItemIdMappings)

        {

            // Upsert items

            foreach (var itemDto in collection.Upsert ?? Enumerable.Empty<SyncExpenseItemDto>())

            {

                ExpenseItem item;

                if (itemDto.RemoteId.HasValue)

                {

                    item = expense.Items.FirstOrDefault(i => i.Id == itemDto.RemoteId.Value);

                    if (item != null)

                    {

                        UpdateExpenseItemFromDto(item, itemDto, memberIdMappings);

                    }

                    else

                    {

                        item = CreateExpenseItemFromDto(expense.Id, itemDto, memberIdMappings);

                        expense.Items.Add(item);

                    }

                }

                else

                {

                    item = CreateExpenseItemFromDto(expense.Id, itemDto, memberIdMappings);

                    expense.Items.Add(item);

                }

                expenseItemIdMappings[itemDto.LocalId] = item.Id;

            }

    

            // Delete items

            foreach (var deletedIdStr in collection.DeletedIds ?? Enumerable.Empty<string>())

            {

                if (Guid.TryParse(deletedIdStr, out var deletedId))

                {

                    var item = expense.Items.FirstOrDefault(i => i.Id == deletedId);

                    if (item != null)

                    {

                        expense.Items.Remove(item);

                    }

                }

            }

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
        expense.PaidById = dto.PaidByLocalId != null &&
                           memberIdMappings.TryGetValue(dto.PaidByLocalId, out var paidById)
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
            bill.Version,
            bill.CreatedAt,
            bill.UpdatedAt,
            bill.Members.Select(m => new MemberDto(
                m.Id,
                m.Name,
                m.OriginalName,
                m.DisplayOrder,
                m.LinkedUserId,
                m.LinkedUser?.DisplayName,
                m.LinkedUser?.AvatarUrl,
                m.ClaimedAt
            )).ToList(),
            bill.Expenses.Select(e => new ExpenseDto(
                e.Id,
                e.Name,
                e.Amount,
                e.ServiceFeePercent,
                e.IsItemized,
                e.PaidById,
                e.Participants.Select(p => p.MemberId).ToList(),
                e.IsItemized
                    ? e.Items.Select(i => new ExpenseItemDto(
                        i.Id,
                        i.Name,
                        i.Amount,
                        i.PaidById,
                        i.Participants.Select(p => p.MemberId).ToList()
                    )).ToList()
                    : null,
                e.CreatedAt
            )).ToList()
        );
    }
}
