using Kayden.Commons.Common;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.SnapSplit;

public class BillService(IUnitOfWork unitOfWork, IBillNotificationService notificationService) : IBillService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IBillNotificationService _notificationService = notificationService;

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
            b.IsSettled,
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
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
            Bill bill;
            var memberIdMappings = new Dictionary<string, Guid>();
            var expenseIdMappings = new Dictionary<string, Guid>();
            var expenseItemIdMappings = new Dictionary<string, Guid>();

            if (dto.RemoteId.HasValue)
            {
                // 更新現有帳單
                var existingBill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(dto.RemoteId.Value, ct);
                if (existingBill == null)
                {
                    return Result.Failure<SyncBillResponseDto>(ErrorCodes.BillNotFound, "Bill not found.");
                }

                bill = existingBill;

                // 樂觀鎖檢查
                if (dto.BaseVersion != bill.Version)
                {
                    // 回傳衝突並附帶最新帳單資料
                    return new SyncBillResponseDto(
                        bill.Id,
                        bill.Version,
                        bill.ShareCode,
                        new SyncIdMappingsDto(
                            new Dictionary<string, Guid>(),
                            new Dictionary<string, Guid>(),
                            new Dictionary<string, Guid>()
                        ),
                        DateTime.UtcNow,
                        MapToBillDto(bill)
                    );
                }
            }
            else
            {
                // 首次同步：建立新帳單
                bill = new Bill
                {
                    Id = Guid.NewGuid(),
                    Name = dto.Name ?? "Untitled",
                    OwnerId = ownerId,
                    ShareCode = GenerateShortCode()
                };
                await _unitOfWork.Bills.AddAsync(bill, ct);
            }

            // 更新帳單名稱（如果有提供）
            if (!string.IsNullOrEmpty(dto.Name))
            {
                bill.Name = dto.Name;
            }

            // 建立現有成員的 RemoteId 映射
            var existingMembersByRemoteId = bill.Members.ToDictionary(m => m.Id);

            // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
            var memberLocalToRemoteMap = dto.Members.Upsert
                .Where(m => m.RemoteId.HasValue)
                .ToDictionary(m => m.LocalId, m => m.RemoteId!.Value);

            // 處理成員刪除
            foreach (var deletedId in dto.Members.DeletedIds)
            {
                Guid? remoteIdToDelete = null;

                // 策略 1: 嘗試將 deletedId 解析為 Guid (直接傳入 RemoteId)
                if (Guid.TryParse(deletedId, out var parsedGuid))
                {
                    remoteIdToDelete = parsedGuid;
                }
                // 策略 2: 從 LocalId -> RemoteId 映射中查找
                else if (memberLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                {
                    remoteIdToDelete = mappedRemoteId;
                }

                // 執行刪除
                if (remoteIdToDelete.HasValue &&
                    existingMembersByRemoteId.TryGetValue(remoteIdToDelete.Value, out var memberToRemove))
                {
                    bill.Members.Remove(memberToRemove);
                }
            }

            // 處理成員 Upsert
            foreach (var memberDto in dto.Members.Upsert)
            {
                if (dto.Members.DeletedIds.Contains(memberDto.LocalId))
                    continue; // 跳過已刪除的

                Member member;
                if (memberDto.RemoteId.HasValue && existingMembersByRemoteId.TryGetValue(memberDto.RemoteId.Value, out member!))
                {
                    // 更新現有成員
                    member.Name = memberDto.Name;
                    member.DisplayOrder = memberDto.DisplayOrder;
                    member.LinkedUserId = memberDto.LinkedUserId;
                    member.ClaimedAt = memberDto.ClaimedAt;
                    member.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // 新增成員
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

            // 建立現有費用的 RemoteId 映射
            var existingExpensesByRemoteId = bill.Expenses.ToDictionary(e => e.Id);

            // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
            var expenseLocalToRemoteMap = dto.Expenses.Upsert
                .Where(e => e.RemoteId.HasValue)
                .ToDictionary(e => e.LocalId, e => e.RemoteId!.Value);

            // 處理費用刪除
            foreach (var deletedId in dto.Expenses.DeletedIds)
            {
                Guid? remoteIdToDelete = null;

                // 策略 1: 嘗試將 deletedId 解析為 Guid (直接傳入 RemoteId)
                if (Guid.TryParse(deletedId, out var parsedGuid))
                {
                    remoteIdToDelete = parsedGuid;
                }
                // 策略 2: 從 LocalId -> RemoteId 映射中查找
                else if (expenseLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                {
                    remoteIdToDelete = mappedRemoteId;
                }

                // 執行刪除
                if (remoteIdToDelete.HasValue &&
                    existingExpensesByRemoteId.TryGetValue(remoteIdToDelete.Value, out var expenseToRemove))
                {
                    bill.Expenses.Remove(expenseToRemove);
                }
            }

            // 處理費用 Upsert
            foreach (var expenseDto in dto.Expenses.Upsert)
            {
                if (dto.Expenses.DeletedIds.Contains(expenseDto.LocalId))
                    continue;

                Expense expense;
                if (expenseDto.RemoteId.HasValue && existingExpensesByRemoteId.TryGetValue(expenseDto.RemoteId.Value, out expense!))
                {
                    // 更新現有費用
                    expense.Name = expenseDto.Name;
                    expense.Amount = expenseDto.Amount;
                    expense.ServiceFeePercent = expenseDto.ServiceFeePercent;
                    expense.IsItemized = expenseDto.IsItemized;
                    expense.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // 新增費用
                    expense = new Expense
                    {
                        Id = Guid.NewGuid(),
                        BillId = bill.Id,
                        Name = expenseDto.Name,
                        Amount = expenseDto.Amount,
                        ServiceFeePercent = expenseDto.ServiceFeePercent,
                        IsItemized = expenseDto.IsItemized
                    };
                    bill.Expenses.Add(expense);
                }

                // 處理付款者
                if (!string.IsNullOrEmpty(expenseDto.PaidByLocalId) && memberIdMappings.TryGetValue(expenseDto.PaidByLocalId, out var paidById))
                {
                    expense.PaidById = paidById;
                }

                // 處理參與者
                expense.Participants.Clear();
                foreach (var participantLocalId in expenseDto.ParticipantLocalIds)
                {
                    if (memberIdMappings.TryGetValue(participantLocalId, out var participantId))
                    {
                        expense.Participants.Add(new ExpenseParticipant
                        {
                            ExpenseId = expense.Id,
                            MemberId = participantId
                        });
                    }
                }

                // 處理費用細項
                if (expenseDto.IsItemized && expenseDto.Items != null)
                {
                    var existingItemsByRemoteId = expense.Items.ToDictionary(i => i.Id);

                    // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
                    var itemLocalToRemoteMap = expenseDto.Items.Upsert
                        .Where(i => i.RemoteId.HasValue)
                        .ToDictionary(i => i.LocalId, i => i.RemoteId!.Value);

                    // 處理細項刪除
                    foreach (var deletedId in expenseDto.Items.DeletedIds)
                    {
                        Guid? remoteIdToDelete = null;

                        // 策略 1: 嘗試將 deletedId 解析為 Guid (直接傳入 RemoteId)
                        if (Guid.TryParse(deletedId, out var parsedGuid))
                        {
                            remoteIdToDelete = parsedGuid;
                        }
                        // 策略 2: 從 LocalId -> RemoteId 映射中查找
                        else if (itemLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                        {
                            remoteIdToDelete = mappedRemoteId;
                        }

                        // 執行刪除
                        if (remoteIdToDelete.HasValue &&
                            existingItemsByRemoteId.TryGetValue(remoteIdToDelete.Value, out var itemToRemove))
                        {
                            expense.Items.Remove(itemToRemove);
                        }
                    }

                    // 處理細項 Upsert
                    foreach (var itemDto in expenseDto.Items.Upsert)
                    {
                        if (expenseDto.Items.DeletedIds.Contains(itemDto.LocalId))
                            continue;

                        ExpenseItem item;
                        if (itemDto.RemoteId.HasValue && existingItemsByRemoteId.TryGetValue(itemDto.RemoteId.Value, out item!))
                        {
                            // 更新現有細項
                            item.Name = itemDto.Name;
                            item.Amount = itemDto.Amount;
                        }
                        else
                        {
                            // 新增細項
                            item = new ExpenseItem
                            {
                                Id = Guid.NewGuid(),
                                ExpenseId = expense.Id,
                                Name = itemDto.Name,
                                Amount = itemDto.Amount
                            };
                            expense.Items.Add(item);
                        }

                        // 處理付款者
                        if (!string.IsNullOrEmpty(itemDto.PaidByLocalId) && memberIdMappings.TryGetValue(itemDto.PaidByLocalId, out var itemPaidById))
                        {
                            item.PaidById = itemPaidById;
                        }

                        // 處理參與者
                        item.Participants.Clear();
                        foreach (var participantLocalId in itemDto.ParticipantLocalIds)
                        {
                            if (memberIdMappings.TryGetValue(participantLocalId, out var participantId))
                            {
                                item.Participants.Add(new ExpenseItemParticipant
                                {
                                    ExpenseItemId = item.Id,
                                    MemberId = participantId
                                });
                            }
                        }

                        expenseItemIdMappings[itemDto.LocalId] = item.Id;
                    }
                }

                expenseIdMappings[expenseDto.LocalId] = expense.Id;
            }

            // 處理已結清轉帳
            // 格式支援：
            // - 舊格式：fromLocalId-toLocalId（金額預設為 0）
            // - 新格式：fromLocalId-toLocalId:amount（包含結清金額快照）
            if (dto.SettledTransfers != null)
            {
                bill.SettledTransfers.Clear();
                foreach (var transfer in dto.SettledTransfers)
                {
                    // 解析金額（如有）
                    var mainPart = transfer;
                    decimal amount = 0;

                    var amountSeparatorIndex = transfer.LastIndexOf(':');
                    if (amountSeparatorIndex > 0 && amountSeparatorIndex < transfer.Length - 1)
                    {
                        var amountStr = transfer.Substring(amountSeparatorIndex + 1);
                        if (decimal.TryParse(amountStr, out var parsedAmount))
                        {
                            amount = parsedAmount;
                            mainPart = transfer.Substring(0, amountSeparatorIndex);
                        }
                    }

                    // 解析 fromId 和 toId
                    var dashIndex = mainPart.IndexOf('-');
                    if (dashIndex > 0 && dashIndex < mainPart.Length - 1)
                    {
                        var fromPart = mainPart.Substring(0, dashIndex);
                        var toPart = mainPart.Substring(dashIndex + 1);

                        // 嘗試從映射表獲取 RemoteId，或直接解析為 Guid
                        Guid? fromId = memberIdMappings.TryGetValue(fromPart, out var mappedFromId)
                            ? mappedFromId
                            : (Guid.TryParse(fromPart, out var parsedFromId) ? parsedFromId : null);

                        Guid? toId = memberIdMappings.TryGetValue(toPart, out var mappedToId)
                            ? mappedToId
                            : (Guid.TryParse(toPart, out var parsedToId) ? parsedToId : null);

                        if (fromId.HasValue && toId.HasValue)
                        {
                            bill.SettledTransfers.Add(new SettledTransfer
                            {
                                BillId = bill.Id,
                                FromMemberId = fromId.Value,
                                ToMemberId = toId.Value,
                                Amount = amount,
                                SettledAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }

            // 更新版本號
            bill.Version++;
            bill.UpdatedAt = DateTime.UtcNow;

            // 不需要呼叫 Update()：
            // - 新增的實體已被 AddAsync 追蹤為 Added
            // - 從資料庫載入的實體會被自動追蹤變更
            await _unitOfWork.SaveChangesAsync(ct);

            return new SyncBillResponseDto(
                bill.Id,
                bill.Version,
                bill.ShareCode,
                new SyncIdMappingsDto(memberIdMappings, expenseIdMappings, expenseItemIdMappings),
                DateTime.UtcNow
            );
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // 資料庫層級的並發衝突：另一個事務在我們讀取和寫入之間修改了 Bill
            // 清除追蹤狀態以避免後續操作受影響
            _unitOfWork.ClearChangeTracker();

            // 重新讀取最新帳單並回傳給前端進行 Rebase
            if (dto.RemoteId.HasValue)
            {
                var latestBill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(dto.RemoteId.Value, ct);
                if (latestBill != null)
                {
                    return new SyncBillResponseDto(
                        latestBill.Id,
                        latestBill.Version,
                        latestBill.ShareCode,
                        new SyncIdMappingsDto(
                            new Dictionary<string, Guid>(),
                            new Dictionary<string, Guid>(),
                            new Dictionary<string, Guid>()
                        ),
                        DateTime.UtcNow,
                        MapToBillDto(latestBill)
                    );
                }
            }

            return Result.Failure<SyncBillResponseDto>(
                ErrorCodes.Conflict,
                "Concurrent modification detected. Please retry with updated version.");
        }
    }

    /// <summary>
    /// Delta 同步帳單資料（v3.2 新機制）
    /// </summary>
    public async Task<Result<DeltaSyncResponse>> DeltaSyncAsync(
        Guid billId,
        DeltaSyncRequest request,
        Guid? userId,
        CancellationToken ct = default)
    {
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var bill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, ct);
                if (bill == null)
                {
                    return Result.Failure<DeltaSyncResponse>(ErrorCodes.BillNotFound, "Bill not found.");
                }

                var conflicts = new List<ConflictInfo>();
                var memberIdMappings = new Dictionary<string, Guid>();
                var expenseIdMappings = new Dictionary<string, Guid>();
                var expenseItemIdMappings = new Dictionary<string, Guid>();

                // 1. 版本檢查
                bool needsCarefulMerge = request.BaseVersion < bill.Version;

                // 2. 處理成員變更
                if (request.Members != null)
                {
                    // 2.1 處理新增成員 (幾乎不會衝突)
                    if (request.Members.Add != null)
                    {
                        foreach (var addDto in request.Members.Add)
                        {
                            var member = new Member
                            {
                                Id = Guid.NewGuid(),
                                BillId = bill.Id,
                                Name = addDto.Name,
                                DisplayOrder = addDto.DisplayOrder ?? (bill.Members.Count > 0 ? bill.Members.Max(m => m.DisplayOrder) + 1 : 0)
                            };
                            bill.Members.Add(member);
                            memberIdMappings[addDto.LocalId] = member.Id;
                        }
                    }

                    // 2.2 處理更新成員
                    if (request.Members.Update != null)
                    {
                        foreach (var updateDto in request.Members.Update)
                        {
                            var member = bill.Members.FirstOrDefault(m => m.Id == updateDto.RemoteId);
                            if (member == null)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "member",
                                    EntityId = updateDto.RemoteId.ToString(),
                                    Resolution = "server_wins",
                                    ServerValue = "deleted",
                                    LocalValue = "update"
                                });
                                continue;
                            }

                            if (needsCarefulMerge)
                            {
                                // 檢查每個欄位是否衝突 (此處簡化處理：若欄位非 null 則嘗試更新，若伺服器已有變動則回報)
                                // 實際實作中，我們需要知道每個欄位在伺服器端上次更新的時間，或是記錄歷史版本。
                                // 這裡先採用規格書建議：伺服器優先。
                                if (updateDto.Name != null && member.Name != updateDto.Name)
                                {
                                    conflicts.Add(new ConflictInfo
                                    {
                                        Type = "member",
                                        EntityId = member.Id.ToString(),
                                        Field = "name",
                                        LocalValue = updateDto.Name,
                                        ServerValue = member.Name,
                                        Resolution = "server_wins"
                                    });
                                }
                                else if (updateDto.DisplayOrder.HasValue)
                                {
                                    member.DisplayOrder = updateDto.DisplayOrder.Value;
                                }
                                
                                // LinkedUserId 和 ClaimedAt 通常由認領邏輯處理，此處僅在無衝突時套用
                                if (updateDto.LinkedUserId.HasValue) member.LinkedUserId = updateDto.LinkedUserId;
                                if (updateDto.ClaimedAt.HasValue) member.ClaimedAt = updateDto.ClaimedAt;
                            }
                            else
                            {
                                if (updateDto.Name != null) member.Name = updateDto.Name;
                                if (updateDto.DisplayOrder.HasValue) member.DisplayOrder = updateDto.DisplayOrder.Value;
                                if (updateDto.LinkedUserId.HasValue) member.LinkedUserId = updateDto.LinkedUserId;
                                if (updateDto.ClaimedAt.HasValue) member.ClaimedAt = updateDto.ClaimedAt;
                                member.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }

                    // 2.3 處理刪除成員
                    if (request.Members.Delete != null)
                    {
                        foreach (var remoteId in request.Members.Delete)
                        {
                            var member = bill.Members.FirstOrDefault(m => m.Id == remoteId);
                            if (member == null) continue; // 已刪除，忽略

                            if (needsCarefulMerge)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "member",
                                    EntityId = remoteId.ToString(),
                                    Resolution = "manual_required",
                                    LocalValue = "delete",
                                    ServerValue = "modified_by_others"
                                });
                            }
                            else
                            {
                                bill.Members.Remove(member);
                            }
                        }
                    }
                }

                // 3. 處理費用變更
                if (request.Expenses != null)
                {
                    // 3.1 處理新增費用
                    if (request.Expenses.Add != null)
                    {
                        foreach (var addDto in request.Expenses.Add)
                        {
                            var expense = new Expense
                            {
                                Id = Guid.NewGuid(),
                                BillId = bill.Id,
                                Name = addDto.Name,
                                Amount = addDto.Amount,
                                ServiceFeePercent = addDto.ServiceFeePercent ?? 0,
                                IsItemized = addDto.IsItemized ?? false
                            };

                            // 付款者處理
                            if (!string.IsNullOrEmpty(addDto.PaidByMemberId))
                            {
                                if (memberIdMappings.TryGetValue(addDto.PaidByMemberId, out var mappedId))
                                    expense.PaidById = mappedId;
                                else if (Guid.TryParse(addDto.PaidByMemberId, out var remoteId))
                                    expense.PaidById = remoteId;
                            }

                            // 參與者處理
                            if (addDto.ParticipantIds != null)
                            {
                                foreach (var pid in addDto.ParticipantIds)
                                {
                                    Guid? memberId = null;
                                    if (memberIdMappings.TryGetValue(pid, out var mappedId))
                                        memberId = mappedId;
                                    else if (Guid.TryParse(pid, out var remoteId))
                                        memberId = remoteId;

                                    if (memberId.HasValue)
                                    {
                                        expense.Participants.Add(new ExpenseParticipant
                                        {
                                            ExpenseId = expense.Id,
                                            MemberId = memberId.Value
                                        });
                                    }
                                }
                            }

                            bill.Expenses.Add(expense);
                            expenseIdMappings[addDto.LocalId] = expense.Id;
                        }
                    }

                    // 3.2 處理更新費用
                    if (request.Expenses.Update != null)
                    {
                        foreach (var updateDto in request.Expenses.Update)
                        {
                            var expense = bill.Expenses.FirstOrDefault(e => e.Id == updateDto.RemoteId);
                            if (expense == null)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expense",
                                    EntityId = updateDto.RemoteId.ToString(),
                                    Resolution = "server_wins",
                                    ServerValue = "deleted"
                                });
                                continue;
                            }

                            if (needsCarefulMerge)
                            {
                                // 簡化衝突檢測：如果本地嘗試修改且伺服器版本不同，標記衝突
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expense",
                                    EntityId = expense.Id.ToString(),
                                    Resolution = "server_wins",
                                    ServerValue = "modified_by_others"
                                });
                            }
                            else
                            {
                                if (updateDto.Name != null) expense.Name = updateDto.Name;
                                if (updateDto.Amount.HasValue) expense.Amount = updateDto.Amount.Value;
                                if (updateDto.ServiceFeePercent.HasValue) expense.ServiceFeePercent = updateDto.ServiceFeePercent.Value;
                                if (updateDto.IsItemized.HasValue) expense.IsItemized = updateDto.IsItemized.Value;
                                
                                if (!string.IsNullOrEmpty(updateDto.PaidByMemberId))
                                {
                                    if (memberIdMappings.TryGetValue(updateDto.PaidByMemberId, out var mappedId))
                                        expense.PaidById = mappedId;
                                    else if (Guid.TryParse(updateDto.PaidByMemberId, out var remoteId))
                                        expense.PaidById = remoteId;
                                }

                                if (updateDto.ParticipantIds != null)
                                {
                                    expense.Participants.Clear();
                                    foreach (var pid in updateDto.ParticipantIds)
                                    {
                                        Guid? memberId = null;
                                        if (memberIdMappings.TryGetValue(pid, out var mappedId))
                                            memberId = mappedId;
                                        else if (Guid.TryParse(pid, out var remoteId))
                                            memberId = remoteId;

                                        if (memberId.HasValue)
                                        {
                                            expense.Participants.Add(new ExpenseParticipant
                                            {
                                                ExpenseId = expense.Id,
                                                MemberId = memberId.Value
                                            });
                                        }
                                    }
                                }
                                expense.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }

                    // 3.3 處理刪除費用
                    if (request.Expenses.Delete != null)
                    {
                        foreach (var remoteId in request.Expenses.Delete)
                        {
                            var expense = bill.Expenses.FirstOrDefault(e => e.Id == remoteId);
                            if (expense == null) continue;

                            if (needsCarefulMerge)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expense",
                                    EntityId = remoteId.ToString(),
                                    Resolution = "manual_required",
                                    ServerValue = "modified_by_others"
                                });
                            }
                            else
                            {
                                bill.Expenses.Remove(expense);
                            }
                        }
                    }
                }

                // 4. 處理費用細項 (與費用邏輯類似)
                if (request.ExpenseItems != null)
                {
                    // 4.1 新增細項
                    if (request.ExpenseItems.Add != null)
                    {
                        foreach (var addDto in request.ExpenseItems.Add)
                        {
                            var expense = bill.Expenses.FirstOrDefault(e => e.Id == addDto.ExpenseId);
                            if (expense == null) continue;

                            var item = new ExpenseItem
                            {
                                Id = Guid.NewGuid(),
                                ExpenseId = expense.Id,
                                Name = addDto.Name,
                                Amount = addDto.Amount
                            };

                            if (!string.IsNullOrEmpty(addDto.PaidByMemberId))
                            {
                                if (memberIdMappings.TryGetValue(addDto.PaidByMemberId, out var mappedId))
                                    item.PaidById = mappedId;
                                else if (Guid.TryParse(addDto.PaidByMemberId, out var remoteId))
                                    item.PaidById = remoteId;
                            }

                            if (addDto.ParticipantIds != null)
                            {
                                foreach (var pid in addDto.ParticipantIds)
                                {
                                    Guid? memberId = null;
                                    if (memberIdMappings.TryGetValue(pid, out var mappedId))
                                        memberId = mappedId;
                                    else if (Guid.TryParse(pid, out var remoteId))
                                        memberId = remoteId;

                                    if (memberId.HasValue)
                                    {
                                        item.Participants.Add(new ExpenseItemParticipant
                                        {
                                            ExpenseItemId = item.Id,
                                            MemberId = memberId.Value
                                        });
                                    }
                                }
                            }

                            expense.Items.Add(item);
                            expenseItemIdMappings[addDto.LocalId] = item.Id;
                        }
                    }

                    // 4.2 更新細項
                    if (request.ExpenseItems.Update != null)
                    {
                        foreach (var updateDto in request.ExpenseItems.Update)
                        {
                            var item = bill.Expenses.SelectMany(e => e.Items).FirstOrDefault(i => i.Id == updateDto.RemoteId);
                            if (item == null)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expenseItem",
                                    EntityId = updateDto.RemoteId.ToString(),
                                    Resolution = "server_wins",
                                    ServerValue = "deleted"
                                });
                                continue;
                            }

                            if (needsCarefulMerge)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expenseItem",
                                    EntityId = item.Id.ToString(),
                                    Resolution = "server_wins",
                                    ServerValue = "modified_by_others"
                                });
                            }
                            else
                            {
                                if (updateDto.Name != null) item.Name = updateDto.Name;
                                if (updateDto.Amount.HasValue) item.Amount = updateDto.Amount.Value;

                                if (!string.IsNullOrEmpty(updateDto.PaidByMemberId))
                                {
                                    if (memberIdMappings.TryGetValue(updateDto.PaidByMemberId, out var mappedId))
                                        item.PaidById = mappedId;
                                    else if (Guid.TryParse(updateDto.PaidByMemberId, out var remoteId))
                                        item.PaidById = remoteId;
                                }

                                if (updateDto.ParticipantIds != null)
                                {
                                    item.Participants.Clear();
                                    foreach (var pid in updateDto.ParticipantIds)
                                    {
                                        Guid? memberId = null;
                                        if (memberIdMappings.TryGetValue(pid, out var mappedId))
                                            memberId = mappedId;
                                        else if (Guid.TryParse(pid, out var remoteId))
                                            memberId = remoteId;

                                        if (memberId.HasValue)
                                        {
                                            item.Participants.Add(new ExpenseItemParticipant
                                            {
                                                ExpenseItemId = item.Id,
                                                MemberId = memberId.Value
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 4.3 刪除細項
                    if (request.ExpenseItems.Delete != null)
                    {
                        foreach (var remoteId in request.ExpenseItems.Delete)
                        {
                            var item = bill.Expenses.SelectMany(e => e.Items).FirstOrDefault(i => i.Id == remoteId);
                            if (item == null) continue;

                            if (needsCarefulMerge)
                            {
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expenseItem",
                                    EntityId = remoteId.ToString(),
                                    Resolution = "manual_required",
                                    ServerValue = "modified_by_others"
                                });
                            }
                            else
                            {
                                var expense = bill.Expenses.First(e => e.Id == item.ExpenseId);
                                expense.Items.Remove(item);
                            }
                        }
                    }
                }

                // 5. 處理結算變更
                if (request.Settlements != null)
                {
                    // 處理 Mark (新增 SettledTransfer)
                    if (request.Settlements.Mark != null)
                    {
                        foreach (var sDto in request.Settlements.Mark)
                        {
                            Guid? fromId = memberIdMappings.TryGetValue(sDto.FromMemberId, out var fMapped) ? fMapped : (Guid.TryParse(sDto.FromMemberId, out var fGuid) ? fGuid : null);
                            Guid? toId = memberIdMappings.TryGetValue(sDto.ToMemberId, out var tMapped) ? tMapped : (Guid.TryParse(sDto.ToMemberId, out var tGuid) ? tGuid : null);

                            if (fromId.HasValue && toId.HasValue)
                            {
                                if (!bill.SettledTransfers.Any(st => st.FromMemberId == fromId && st.ToMemberId == toId))
                                {
                                    bill.SettledTransfers.Add(new SettledTransfer
                                    {
                                        BillId = bill.Id,
                                        FromMemberId = fromId.Value,
                                        ToMemberId = toId.Value,
                                        Amount = sDto.Amount,
                                        SettledAt = DateTime.UtcNow
                                    });
                                }
                            }
                        }
                    }

                    // 處理 Unmark (刪除 SettledTransfer)
                    if (request.Settlements.Unmark != null)
                    {
                        foreach (var sDto in request.Settlements.Unmark)
                        {
                            Guid? fromId = Guid.TryParse(sDto.FromMemberId, out var fGuid) ? fGuid : null;
                            Guid? toId = Guid.TryParse(sDto.ToMemberId, out var tGuid) ? tGuid : null;

                            if (fromId.HasValue && toId.HasValue)
                            {
                                var existing = bill.SettledTransfers.FirstOrDefault(st => st.FromMemberId == fromId && st.ToMemberId == toId);
                                if (existing != null) bill.SettledTransfers.Remove(existing);
                            }
                        }
                    }
                }

                // 6. 更新版本與儲存
                bill.Version++;
                bill.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(ct);

                // 發送 SignalR 通知
                await _notificationService.NotifyBillUpdatedAsync(bill.Id, bill.Version, userId);

                return new DeltaSyncResponse
                {
                    Success = conflicts.Count == 0,
                    NewVersion = bill.Version,
                    IdMappings = new DeltaIdMappingsDto
                    {
                        Members = memberIdMappings,
                        Expenses = expenseIdMappings,
                        ExpenseItems = expenseItemIdMappings
                    },
                    Conflicts = conflicts.Count > 0 ? conflicts : null,
                    MergedBill = conflicts.Count > 0 ? MapToBillDto(bill) : null
                };
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _unitOfWork.ClearChangeTracker();
            var latestBill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, ct);
            return new DeltaSyncResponse
            {
                Success = false,
                NewVersion = latestBill?.Version ?? 0,
                MergedBill = latestBill != null ? MapToBillDto(latestBill) : null
            };
        }
    }

    private static string GenerateShortCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
    }

    private static BillDto MapToBillDto(Bill bill)
    {
        return new BillDto(
            bill.Id,
            bill.Name,
            bill.ShareCode,
            bill.Version,
            bill.IsSettled,
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
