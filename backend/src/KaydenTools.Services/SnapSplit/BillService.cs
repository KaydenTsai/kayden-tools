using Kayden.Commons.Common;
using Kayden.Commons.Extensions;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.SnapSplit;

public class BillService(IUnitOfWork unitOfWork, IBillNotificationService notificationService) : IBillService
{
    private readonly IBillNotificationService _notificationService = notificationService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    #region IBillService Members

    public async Task<Result<BillDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(id, ct);
        if (bill == null) return Result.Failure<BillDto>(ErrorCodes.BillNotFound, "Bill not found.");

        return MapToBillDto(bill);
    }

    public async Task<Result<BillDto>> GetByShareCodeAsync(string shareCode, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByShareCodeAsync(shareCode, ct);
        if (bill == null) return Result.Failure<BillDto>(ErrorCodes.InvalidShareCode, "Bill not found.");

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
        if (bill == null) return Result.Failure<BillDto>(ErrorCodes.BillNotFound, "Bill not found.");

        bill.Name = dto.Name;
        bill.Version++; // 更新版本號以觸發協作者同步
        bill.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Bills.Update(bill);
        await _unitOfWork.SaveChangesAsync(ct);

        // 發送 SignalR 通知讓其他協作者知道帳單已更新
        await _notificationService.NotifyBillUpdatedAsync(bill.Id, bill.Version, null);

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(id, ct);
        if (bill == null) return Result.Failure(ErrorCodes.BillNotFound, "Bill not found.");

        _unitOfWork.Bills.Remove(bill);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<string>> GenerateShareCodeAsync(Guid id, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdAsync(id, ct);
        if (bill == null) return Result.Failure<string>(ErrorCodes.BillNotFound, "Bill not found.");

        if (!string.IsNullOrEmpty(bill.ShareCode)) return bill.ShareCode;

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
        // 用於儲存通知參數（在 transaction commit 後發送）
        Guid? notifyBillId = null;
        long notifyVersion = 0;

        try
        {
            var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                Bill bill;
                var memberIdMappings = new Dictionary<string, Guid>();
                var expenseIdMappings = new Dictionary<string, Guid>();
                var expenseItemIdMappings = new Dictionary<string, Guid>();

                // 追蹤是否有版本衝突（用於決定回應策略）
                var hasVersionConflict = false;

                if (dto.RemoteId.HasValue)
                {
                    // 情況 1：有 RemoteId → 更新現有帳單
                    var existingBill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(dto.RemoteId.Value, ct);
                    if (existingBill == null)
                        return Result.Failure<SyncBillResponseDto>(ErrorCodes.BillNotFound, "Bill not found.");

                    bill = existingBill;

                    // 樂觀鎖檢查：只有當客戶端版本落後時才觸發衝突
                    // 使用 < 而非 != 以允許客戶端使用當前版本重試
                    if (dto.BaseVersion < bill.Version)
                    {
                        // 標記有衝突，但不立即返回！
                        // 我們會繼續處理新增操作（ADD），只跳過更新操作（UPDATE）
                        // 這樣雙向並發編輯時，新增的項目不會丟失
                        hasVersionConflict = true;
                    }
                }
                else
                {
                    // 情況 2：無 RemoteId → 首次同步
                    // 冪等性檢查：使用 LocalId + OwnerId 查找是否已存在
                    Bill? existingBill = null;
                    if (!string.IsNullOrEmpty(dto.LocalId) && ownerId.HasValue)
                    {
                        existingBill = await _unitOfWork.Bills.GetByLocalClientIdAndOwnerAsync(dto.LocalId, ownerId.Value, ct);
                    }

                    if (existingBill != null)
                    {
                        // 冪等情況：帳單已存在（之前的請求已建立）
                        // 直接回傳現有帳單，前端應使用 LatestBill 重建狀態
                        // 注意：無法重建 LocalId -> RemoteId 映射（因為成員未儲存 LocalId）
                        return new SyncBillResponseDto(
                            existingBill.Id,
                            existingBill.Version,
                            existingBill.ShareCode,
                            new SyncIdMappingsDto(
                                new Dictionary<string, Guid>(),
                                new Dictionary<string, Guid>(),
                                new Dictionary<string, Guid>()
                            ),
                            DateTime.UtcNow,
                            MapToBillDto(existingBill) // 前端應使用此資料重建狀態
                        );
                    }
                    else
                    {
                        // 真正的首次同步：建立新帳單
                        bill = new Bill
                        {
                            Id = Guid.NewGuid(),
                            Name = dto.Name ?? "Untitled",
                            OwnerId = ownerId,
                            LocalClientId = dto.LocalId, // 儲存以供冪等性檢查
                            ShareCode = GenerateShortCode()
                        };
                        await _unitOfWork.Bills.AddAsync(bill, ct);
                    }
                }

                // 更新帳單名稱（如果有提供）- 版本衝突時跳過
                if (!hasVersionConflict && !string.IsNullOrEmpty(dto.Name))
                    bill.Name = dto.Name;

                // 建立現有成員的 RemoteId 映射
                // 注意：使用 DistinctBy 避免 EF Core 關係修復導致的重複實體
                var existingMembersByRemoteId = bill.Members
                    .DistinctBy(m => m.Id)
                    .ToDictionary(m => m.Id);

                // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
                var memberLocalToRemoteMap = dto.Members.Upsert
                    .Where(m => m.RemoteId.HasValue)
                    .ToDictionary(m => m.LocalId, m => m.RemoteId!.Value);

                // 處理成員刪除 - 版本衝突時跳過刪除操作
                if (!hasVersionConflict)
                {
                    foreach (var deletedId in dto.Members.DeletedIds)
                    {
                        Guid? remoteIdToDelete = null;

                        // 策略 1: 嘗試將 deletedId 解析為 Guid (直接傳入 RemoteId)
                        if (Guid.TryParse(deletedId, out var parsedGuid))
                            remoteIdToDelete = parsedGuid;
                        // 策略 2: 從 LocalId -> RemoteId 映射中查找
                        else if (memberLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                            remoteIdToDelete = mappedRemoteId;

                        // 執行刪除
                        if (remoteIdToDelete.HasValue &&
                            existingMembersByRemoteId.TryGetValue(remoteIdToDelete.Value, out var memberToRemove))
                            bill.Members.Remove(memberToRemove);
                    }
                }

                // 處理成員 Upsert
                foreach (var memberDto in dto.Members.Upsert)
                {
                    if (dto.Members.DeletedIds.Contains(memberDto.LocalId))
                        continue; // 跳過已刪除的

                    Member member;
                    if (memberDto.RemoteId.HasValue &&
                        existingMembersByRemoteId.TryGetValue(memberDto.RemoteId.Value, out member!))
                    {
                        // 更新現有成員 - 版本衝突時跳過更新操作
                        if (!hasVersionConflict)
                        {
                            member.Name = memberDto.Name;
                            member.DisplayOrder = memberDto.DisplayOrder;
                            member.LinkedUserId = memberDto.LinkedUserId;
                            member.ClaimedAt = memberDto.ClaimedAt;
                            member.UpdatedAt = DateTime.UtcNow;
                        }
                        // 即使有衝突，仍需建立映射（供後續費用處理使用）
                    }
                    else
                    {
                        // 新增成員 - 即使版本衝突也要處理新增！
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
                // 注意：使用 DistinctBy 避免 EF Core 關係修復導致的重複實體
                var existingExpensesByRemoteId = bill.Expenses
                    .DistinctBy(e => e.Id)
                    .ToDictionary(e => e.Id);

                // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
                var expenseLocalToRemoteMap = dto.Expenses.Upsert
                    .Where(e => e.RemoteId.HasValue)
                    .ToDictionary(e => e.LocalId, e => e.RemoteId!.Value);

                // 處理費用刪除 - 版本衝突時跳過刪除操作
                if (!hasVersionConflict)
                {
                    foreach (var deletedId in dto.Expenses.DeletedIds)
                    {
                        Guid? remoteIdToDelete = null;

                        // 策略 1: 嘗試將 deletedId 解析為 Guid (直接傳入 RemoteId)
                        if (Guid.TryParse(deletedId, out var parsedGuid))
                            remoteIdToDelete = parsedGuid;
                        // 策略 2: 從 LocalId -> RemoteId 映射中查找
                        else if (expenseLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                            remoteIdToDelete = mappedRemoteId;

                        // 執行刪除
                        if (remoteIdToDelete.HasValue &&
                            existingExpensesByRemoteId.TryGetValue(remoteIdToDelete.Value, out var expenseToRemove))
                            bill.Expenses.Remove(expenseToRemove);
                    }
                }

                // 處理費用 Upsert
                foreach (var expenseDto in dto.Expenses.Upsert)
                {
                    if (dto.Expenses.DeletedIds.Contains(expenseDto.LocalId))
                        continue;

                    Expense? expense = null;
                    var isExistingExpense = expenseDto.RemoteId.HasValue &&
                        existingExpensesByRemoteId.TryGetValue(expenseDto.RemoteId.Value, out expense);

                    if (isExistingExpense && expense != null)
                    {
                        // 更新現有費用 - 版本衝突時跳過更新操作
                        if (!hasVersionConflict)
                        {
                            expense.Name = expenseDto.Name;
                            expense.Amount = expenseDto.Amount;
                            expense.ServiceFeePercent = expenseDto.ServiceFeePercent;
                            expense.IsItemized = expenseDto.IsItemized;
                            expense.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        // 新增費用 - 即使版本衝突也要處理新增！
                        expense = new Expense
                        {
                            Id = Guid.NewGuid(),
                            BillId = bill.Id,
                            Name = expenseDto.Name,
                            Amount = expenseDto.Amount,
                            ServiceFeePercent = expenseDto.ServiceFeePercent,
                            IsItemized = expenseDto.IsItemized
                        };
                        await _unitOfWork.Expenses.AddAsync(expense);
                        // 注意：不需要手動添加到導航屬性，EF Core 會自動處理
                    }

                    // 處理付款者和參與者
                    // 版本衝突時只處理新增的費用，跳過更新現有費用的關聯
                    if (!hasVersionConflict || !isExistingExpense)
                    {
                        if (!string.IsNullOrEmpty(expenseDto.PaidByLocalId) &&
                            memberIdMappings.TryGetValue(expenseDto.PaidByLocalId, out var paidById))
                            expense!.PaidById = paidById;

                        expense!.Participants.Clear();
                        // 收集有效的參與者 ID
                        var validParticipantIds = expenseDto.ParticipantLocalIds
                            .Where(lid => memberIdMappings.ContainsKey(lid))
                            .Select(lid => memberIdMappings[lid])
                            .ToList();

                        if (validParticipantIds.Count > 0)
                        {
                            // 計算含服務費總金額並使用 Penny Allocation 分配
                            var totalWithFee = MoneyHelper.CalculateAmountWithServiceFee(
                                expense.Amount, expense.ServiceFeePercent);
                            var amounts = MoneyHelper.Allocate(totalWithFee, validParticipantIds.Count);

                            for (var i = 0; i < validParticipantIds.Count; i++)
                            {
                                expense.Participants.Add(new ExpenseParticipant
                                {
                                    ExpenseId = expense.Id,
                                    Expense = expense,
                                    MemberId = validParticipantIds[i],
                                    Amount = amounts[i]
                                });
                            }
                        }
                    }

                    // 處理費用細項
                    if (expenseDto.IsItemized && expenseDto.Items != null)
                    {
                        // 注意：使用 DistinctBy 避免 EF Core 關係修復導致的重複實體
                        var existingItemsByRemoteId = expense.Items
                            .DistinctBy(i => i.Id)
                            .ToDictionary(i => i.Id);

                        // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
                        var itemLocalToRemoteMap = expenseDto.Items.Upsert
                            .Where(i => i.RemoteId.HasValue)
                            .ToDictionary(i => i.LocalId, i => i.RemoteId!.Value);

                        // 處理細項刪除 - 版本衝突時跳過刪除操作
                        if (!hasVersionConflict)
                        {
                            foreach (var deletedId in expenseDto.Items.DeletedIds)
                            {
                                Guid? remoteIdToDelete = null;

                                // 策略 1: 嘗試將 deletedId 解析為 Guid (直接傳入 RemoteId)
                                if (Guid.TryParse(deletedId, out var parsedGuid))
                                    remoteIdToDelete = parsedGuid;
                                // 策略 2: 從 LocalId -> RemoteId 映射中查找
                                else if (itemLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                                    remoteIdToDelete = mappedRemoteId;

                                // 執行刪除
                                if (remoteIdToDelete.HasValue &&
                                    existingItemsByRemoteId.TryGetValue(remoteIdToDelete.Value, out var itemToRemove))
                                    expense.Items.Remove(itemToRemove);
                            }
                        }

                        // 處理細項 Upsert
                        foreach (var itemDto in expenseDto.Items.Upsert)
                        {
                            if (expenseDto.Items.DeletedIds.Contains(itemDto.LocalId))
                                continue;

                            ExpenseItem? item = null;
                            var isExistingItem = itemDto.RemoteId.HasValue &&
                                existingItemsByRemoteId.TryGetValue(itemDto.RemoteId.Value, out item);

                            if (isExistingItem && item != null)
                            {
                                // 更新現有細項 - 版本衝突時跳過更新操作
                                if (!hasVersionConflict)
                                {
                                    item.Name = itemDto.Name;
                                    item.Amount = itemDto.Amount;
                                }
                            }
                            else
                            {
                                // 新增細項 - 即使版本衝突也要處理新增！
                                item = new ExpenseItem
                                {
                                    Id = Guid.NewGuid(),
                                    ExpenseId = expense!.Id, // 明確設定 FK
                                    Expense = expense,
                                    Name = itemDto.Name,
                                    Amount = itemDto.Amount
                                };
                                expense.Items.Add(item);
                            }

                            // 處理付款者和參與者
                            // 版本衝突時只處理新增的細項，跳過更新現有細項的關聯
                            if (!hasVersionConflict || !isExistingItem)
                            {
                                if (!string.IsNullOrEmpty(itemDto.PaidByLocalId) &&
                                    memberIdMappings.TryGetValue(itemDto.PaidByLocalId, out var itemPaidById))
                                    item!.PaidById = itemPaidById;

                                // 處理參與者
                                item!.Participants.Clear();
                                // 收集有效的參與者 ID
                                var validItemParticipantIds = itemDto.ParticipantLocalIds
                                    .Where(lid => memberIdMappings.ContainsKey(lid))
                                    .Select(lid => memberIdMappings[lid])
                                    .ToList();

                                if (validItemParticipantIds.Count > 0)
                                {
                                    // 使用 Penny Allocation 分配細項金額
                                    var itemAmounts = MoneyHelper.Allocate(item.Amount, validItemParticipantIds.Count);

                                    for (var i = 0; i < validItemParticipantIds.Count; i++)
                                    {
                                        item.Participants.Add(new ExpenseItemParticipant
                                        {
                                            ExpenseItemId = item.Id, // 明確設定 FK
                                            ExpenseItem = item,
                                            MemberId = validItemParticipantIds[i],
                                            Amount = itemAmounts[i]
                                        });
                                    }
                                }
                            }

                            expenseItemIdMappings[itemDto.LocalId] = item!.Id;
                        }
                    }

                    expenseIdMappings[expenseDto.LocalId] = expense!.Id;
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
                                : Guid.TryParse(fromPart, out var parsedFromId)
                                    ? parsedFromId
                                    : null;

                            Guid? toId = memberIdMappings.TryGetValue(toPart, out var mappedToId)
                                ? mappedToId
                                : Guid.TryParse(toPart, out var parsedToId)
                                    ? parsedToId
                                    : null;

                            if (fromId.HasValue && toId.HasValue)
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

                // 更新版本號
                bill.Version++;
                bill.UpdatedAt = DateTime.UtcNow;

                // 不需要呼叫 Update()：
                // - 新增的實體已被 AddAsync 追蹤為 Added
                // - 從資料庫載入的實體會被自動追蹤變更
                await _unitOfWork.SaveChangesAsync(ct);

                // 儲存通知參數（在 transaction commit 後發送）
                notifyBillId = bill.Id;
                notifyVersion = bill.Version;

                // 版本衝突時返回 LatestBill 讓前端可以 rebase
                // 新增操作已合併，前端需用 LatestBill 重建狀態以取得正確的 RemoteId 映射
                return new SyncBillResponseDto(
                    bill.Id,
                    bill.Version,
                    bill.ShareCode,
                    new SyncIdMappingsDto(memberIdMappings, expenseIdMappings, expenseItemIdMappings),
                    DateTime.UtcNow,
                    hasVersionConflict ? MapToBillDto(bill) : null
                );
            }, ct);

            // Transaction 已成功 commit，現在發送 SignalR 通知
            if (notifyBillId.HasValue)
            {
                await _notificationService.NotifyBillUpdatedAsync(notifyBillId.Value, notifyVersion, ownerId);
            }

            return result;
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

            return Result.Failure<SyncBillResponseDto>(
                ErrorCodes.Conflict,
                "Concurrent modification detected. Please retry with updated version.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // 唯一索引衝突：兩個請求同時嘗試建立相同 LocalClientId 的帳單
            // 這是競態條件，表示另一個請求已成功建立帳單
            _unitOfWork.ClearChangeTracker();

            // 重新查詢已存在的帳單
            if (!string.IsNullOrEmpty(dto.LocalId) && ownerId.HasValue)
            {
                var existingBill = await _unitOfWork.Bills.GetByLocalClientIdAndOwnerAsync(dto.LocalId, ownerId.Value, ct);
                if (existingBill != null)
                    return new SyncBillResponseDto(
                        existingBill.Id,
                        existingBill.Version,
                        existingBill.ShareCode,
                        new SyncIdMappingsDto(
                            new Dictionary<string, Guid>(),
                            new Dictionary<string, Guid>(),
                            new Dictionary<string, Guid>()
                        ),
                        DateTime.UtcNow,
                        MapToBillDto(existingBill)
                    );
            }

            return Result.Failure<SyncBillResponseDto>(
                ErrorCodes.Conflict,
                "A bill with this local ID already exists.");
        }
    }

    /// <summary>
    /// 檢查是否為唯一索引衝突異常
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL 唯一索引衝突錯誤碼為 23505
        // 檢查內部異常是否為 PostgresException 且錯誤碼為 23505
        if (ex.InnerException is Npgsql.PostgresException pgEx)
            return pgEx.SqlState == "23505";

        // 其他資料庫可能有不同的錯誤碼
        return ex.InnerException?.Message.Contains("unique constraint") == true ||
               ex.InnerException?.Message.Contains("duplicate key") == true;
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
        // 用於儲存通知參數（在 transaction commit 後發送）
        Guid? notifyBillId = null;
        long notifyVersion = 0;

        try
        {
            var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var bill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, ct);
                if (bill == null) return Result.Failure<DeltaSyncResponse>(ErrorCodes.BillNotFound, "Bill not found.");

                var conflicts = new List<ConflictInfo>();
                var memberIdMappings = new Dictionary<string, Guid>();
                var expenseIdMappings = new Dictionary<string, Guid>();
                var expenseItemIdMappings = new Dictionary<string, Guid>();

                // 1. 版本檢查
                var needsCarefulMerge = request.BaseVersion < bill.Version;

                // 2. 處理成員變更
                if (request.Members != null)
                {
                    // 2.1 處理新增成員 (幾乎不會衝突)
                    if (request.Members.Add != null)
                        foreach (var addDto in request.Members.Add)
                        {
                            // 冪等性檢查：同一請求內是否已處理過相同 LocalId
                            if (memberIdMappings.ContainsKey(addDto.LocalId))
                                continue;

                            // 冪等性檢查：資料庫中是否已存在相同 LocalClientId 的成員
                            var existingMember = bill.Members.FirstOrDefault(m => m.LocalClientId == addDto.LocalId);
                            if (existingMember != null)
                            {
                                // 已存在，直接回傳現有 ID（不建立新成員）
                                memberIdMappings[addDto.LocalId] = existingMember.Id;
                                continue;
                            }

                            var member = new Member
                            {
                                Id = Guid.NewGuid(),
                                BillId = bill.Id,
                                LocalClientId = addDto.LocalId, // 儲存 LocalId 以供冪等性檢查
                                Name = addDto.Name,
                                DisplayOrder = addDto.DisplayOrder ??
                                               (bill.Members.Count > 0 ? bill.Members.Max(m => m.DisplayOrder) + 1 : 0),
                                // 支援新增時帶認領資訊
                                LinkedUserId = addDto.LinkedUserId,
                                ClaimedAt = addDto.ClaimedAt
                            };
                            // 使用 Repository 明確加入成員（確保 EF Core 正確追蹤，與費用處理方式一致）
                            await _unitOfWork.Members.AddAsync(member);
                            bill.Members.Add(member); // 同時更新導航屬性以便後續計算 DisplayOrder
                            memberIdMappings[addDto.LocalId] = member.Id;
                        }

                    // 2.2 處理更新成員
                    if (request.Members.Update != null)
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
                                    conflicts.Add(new ConflictInfo
                                    {
                                        Type = "member",
                                        EntityId = member.Id.ToString(),
                                        Field = "name",
                                        LocalValue = updateDto.Name,
                                        ServerValue = member.Name,
                                        Resolution = "server_wins"
                                    });
                                else if (updateDto.DisplayOrder.HasValue)
                                    member.DisplayOrder = updateDto.DisplayOrder.Value;

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

                    // 2.3 處理刪除成員
                    if (request.Members.Delete != null)
                        foreach (var remoteId in request.Members.Delete)
                        {
                            var member = bill.Members.FirstOrDefault(m => m.Id == remoteId);
                            if (member == null) continue; // 已刪除，忽略

                            if (needsCarefulMerge)
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "member",
                                    EntityId = remoteId.ToString(),
                                    Resolution = "manual_required",
                                    LocalValue = "delete",
                                    ServerValue = "modified_by_others"
                                });
                            else
                            {
                                // 刪除引用該成員的 SettledTransfer 記錄（FK 設定為 Restrict）
                                // 必須同時：1) 從集合中移除 2) 用 Repository 標記刪除
                                // 順序很重要：先從集合移除，避免 NavigationFixer 在 Remove member 時處理這些 orphans
                                var transfersToRemove = bill.SettledTransfers
                                    .Where(st => st.FromMemberId == remoteId || st.ToMemberId == remoteId)
                                    .ToList();
                                foreach (var transfer in transfersToRemove)
                                {
                                    bill.SettledTransfers.Remove(transfer);
                                    _unitOfWork.SettledTransfers.Remove(transfer);
                                }

                                bill.Members.Remove(member);
                            }
                        }
                }

                // 2.5 建立有效成員 ID 集合（用於驗證幽靈參照）
                var validMemberIds = bill.Members.Select(m => m.Id).ToHashSet();

                // Helper: 驗證成員 ID 是否有效
                Guid? ResolveMemberId(string? idString)
                {
                    if (string.IsNullOrEmpty(idString)) return null;
                    if (memberIdMappings.TryGetValue(idString, out var mappedId)) return mappedId;
                    if (Guid.TryParse(idString, out var remoteId) && validMemberIds.Contains(remoteId)) return remoteId;
                    return null;
                }

                // Helper: 驗證一組成員 ID，回傳無效的 ID 列表
                List<string> ValidateParticipantIds(IEnumerable<string>? participantIds)
                {
                    if (participantIds == null) return new List<string>();
                    var invalidIds = new List<string>();
                    foreach (var pid in participantIds)
                    {
                        if (ResolveMemberId(pid) == null)
                            invalidIds.Add(pid);
                    }
                    return invalidIds;
                }

                // 3. 處理費用變更
                if (request.Expenses != null)
                {
                    // 3.1 處理新增費用
                    if (request.Expenses.Add != null)
                        foreach (var addDto in request.Expenses.Add)
                        {
                            // 冪等性檢查：同一請求內是否已處理過相同 LocalId
                            if (expenseIdMappings.ContainsKey(addDto.LocalId))
                                continue;

                            // 冪等性檢查：資料庫中是否已存在相同 LocalClientId 的費用
                            var existingExpense = bill.Expenses.FirstOrDefault(e => e.LocalClientId == addDto.LocalId);
                            if (existingExpense != null)
                            {
                                // 已存在，直接回傳現有 ID（不建立新費用）
                                expenseIdMappings[addDto.LocalId] = existingExpense.Id;
                                continue;
                            }

                            // 驗證 PaidByMemberId（幽靈參照檢查）
                            if (!string.IsNullOrEmpty(addDto.PaidByMemberId) && ResolveMemberId(addDto.PaidByMemberId) == null)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"費用 '{addDto.Name}' 的付款者 ID '{addDto.PaidByMemberId}' 無效或已刪除");

                            // 驗證 ParticipantIds（幽靈參照檢查）
                            var invalidParticipantIds = ValidateParticipantIds(addDto.ParticipantIds);
                            if (invalidParticipantIds.Count > 0)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"費用 '{addDto.Name}' 包含無效的參與者 ID: {string.Join(", ", invalidParticipantIds)}");

                            var expense = new Expense
                            {
                                Id = Guid.NewGuid(),
                                BillId = bill.Id,
                                LocalClientId = addDto.LocalId, // 儲存 LocalId 以供冪等性檢查
                                Name = addDto.Name,
                                Amount = addDto.Amount,
                                ServiceFeePercent = addDto.ServiceFeePercent ?? 0,
                                IsItemized = addDto.IsItemized ?? false
                            };

                            // 付款者處理（已驗證有效）
                            expense.PaidById = ResolveMemberId(addDto.PaidByMemberId);

                            // 使用 Repository 明確加入費用（確保 EF Core 正確追蹤）
                            await _unitOfWork.Expenses.AddAsync(expense);
                            bill.Expenses.Add(expense); // 同時更新導航屬性
                            expenseIdMappings[addDto.LocalId] = expense.Id;

                            // 參與者處理（已驗證有效，費用已被追蹤，EF Core 可正確排序 INSERT）
                            if (addDto.ParticipantIds != null)
                            {
                                // 收集有效的參與者 ID（已通過驗證）
                                var validExpenseParticipantIds = addDto.ParticipantIds
                                    .Select(pid => ResolveMemberId(pid)!.Value)
                                    .ToList();

                                if (validExpenseParticipantIds.Count > 0)
                                {
                                    // 計算含服務費總金額並使用 Penny Allocation 分配
                                    var totalWithFee = MoneyHelper.CalculateAmountWithServiceFee(
                                        expense.Amount, expense.ServiceFeePercent);
                                    var amounts = MoneyHelper.Allocate(totalWithFee, validExpenseParticipantIds.Count);

                                    for (var i = 0; i < validExpenseParticipantIds.Count; i++)
                                    {
                                        expense.Participants.Add(new ExpenseParticipant
                                        {
                                            ExpenseId = expense.Id,
                                            Expense = expense,
                                            MemberId = validExpenseParticipantIds[i],
                                            Amount = amounts[i]
                                        });
                                    }
                                }
                            }
                        }

                    // 3.2 處理更新費用
                    if (request.Expenses.Update != null)
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

                            // 驗證 PaidByMemberId（幽靈參照檢查 - 無論版本衝突與否都要檢查）
                            if (!string.IsNullOrEmpty(updateDto.PaidByMemberId) && ResolveMemberId(updateDto.PaidByMemberId) == null)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"費用更新的付款者 ID '{updateDto.PaidByMemberId}' 無效或已刪除");

                            // 驗證 ParticipantIds（幽靈參照檢查 - 無論版本衝突與否都要檢查）
                            var invalidParticipantIds = ValidateParticipantIds(updateDto.ParticipantIds);
                            if (invalidParticipantIds.Count > 0)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"費用更新包含無效的參與者 ID: {string.Join(", ", invalidParticipantIds)}");

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
                                if (updateDto.ServiceFeePercent.HasValue)
                                    expense.ServiceFeePercent = updateDto.ServiceFeePercent.Value;
                                if (updateDto.IsItemized.HasValue) expense.IsItemized = updateDto.IsItemized.Value;

                                // 付款者處理（已驗證有效）
                                if (!string.IsNullOrEmpty(updateDto.PaidByMemberId))
                                    expense.PaidById = ResolveMemberId(updateDto.PaidByMemberId);

                                if (updateDto.ParticipantIds != null)
                                {
                                    // 收集有效的參與者 ID（已通過驗證，保持順序以配合 Penny Allocation）
                                    var newMemberIds = updateDto.ParticipantIds
                                        .Select(pid => ResolveMemberId(pid)!.Value)
                                        .ToList();

                                    // 清除現有參與者並重新計算金額
                                    expense.Participants.Clear();
                                    if (newMemberIds.Count > 0)
                                    {
                                        // 計算含服務費總金額並使用 Penny Allocation 分配
                                        var totalWithFee = MoneyHelper.CalculateAmountWithServiceFee(
                                            expense.Amount, expense.ServiceFeePercent);
                                        var amounts = MoneyHelper.Allocate(totalWithFee, newMemberIds.Count);

                                        for (var i = 0; i < newMemberIds.Count; i++)
                                        {
                                            expense.Participants.Add(new ExpenseParticipant
                                            {
                                                ExpenseId = expense.Id,
                                                Expense = expense,
                                                MemberId = newMemberIds[i],
                                                Amount = amounts[i]
                                            });
                                        }
                                    }
                                }

                                expense.UpdatedAt = DateTime.UtcNow;
                            }
                        }

                    // 3.3 處理刪除費用
                    if (request.Expenses.Delete != null)
                        foreach (var remoteId in request.Expenses.Delete)
                        {
                            var expense = bill.Expenses.FirstOrDefault(e => e.Id == remoteId);
                            if (expense == null) continue;

                            if (needsCarefulMerge)
                                conflicts.Add(new ConflictInfo
                                {
                                    Type = "expense",
                                    EntityId = remoteId.ToString(),
                                    Resolution = "manual_required",
                                    ServerValue = "modified_by_others"
                                });
                            else
                                bill.Expenses.Remove(expense);
                        }
                }

                // 4. 處理費用細項 (與費用邏輯類似)
                if (request.ExpenseItems != null)
                {
                    // 4.1 新增細項
                    if (request.ExpenseItems.Add != null)
                        foreach (var addDto in request.ExpenseItems.Add)
                        {
                            // 冪等性檢查：同一請求內是否已處理過相同 LocalId
                            if (expenseItemIdMappings.ContainsKey(addDto.LocalId))
                                continue;

                            // 解析 ExpenseId：可能是 LocalId（同次請求新增的費用）或 RemoteId（已存在的費用）
                            Guid expenseGuid;
                            if (expenseIdMappings.TryGetValue(addDto.ExpenseId, out var mappedExpenseId))
                                expenseGuid = mappedExpenseId;
                            else if (!Guid.TryParse(addDto.ExpenseId, out expenseGuid))
                                continue; // 無效的 ID 格式

                            var expense = bill.Expenses.FirstOrDefault(e => e.Id == expenseGuid);
                            if (expense == null) continue;

                            // 冪等性檢查：資料庫中是否已存在相同 LocalClientId 的細項
                            var existingItem = expense.Items.FirstOrDefault(i => i.LocalClientId == addDto.LocalId);
                            if (existingItem != null)
                            {
                                // 已存在，直接回傳現有 ID（不建立新細項）
                                expenseItemIdMappings[addDto.LocalId] = existingItem.Id;
                                continue;
                            }

                            // 驗證 PaidByMemberId（幽靈參照檢查）
                            if (!string.IsNullOrEmpty(addDto.PaidByMemberId) && ResolveMemberId(addDto.PaidByMemberId) == null)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"細項 '{addDto.Name}' 的付款者 ID '{addDto.PaidByMemberId}' 無效或已刪除");

                            // 驗證 ParticipantIds（幽靈參照檢查）
                            var invalidItemParticipantIds = ValidateParticipantIds(addDto.ParticipantIds);
                            if (invalidItemParticipantIds.Count > 0)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"細項 '{addDto.Name}' 包含無效的參與者 ID: {string.Join(", ", invalidItemParticipantIds)}");

                            var item = new ExpenseItem
                            {
                                Id = Guid.NewGuid(),
                                ExpenseId = expense.Id, // 明確設定 FK
                                Expense = expense,
                                LocalClientId = addDto.LocalId, // 儲存 LocalId 以供冪等性檢查
                                Name = addDto.Name,
                                Amount = addDto.Amount
                            };

                            // 付款者處理（已驗證有效）
                            item.PaidById = ResolveMemberId(addDto.PaidByMemberId);

                            // 使用 Repository 明確加入細項（確保 EF Core 正確追蹤，與費用處理方式一致）
                            await _unitOfWork.ExpenseItems.AddAsync(item);
                            expense.Items.Add(item); // 同時更新導航屬性
                            expenseItemIdMappings[addDto.LocalId] = item.Id;

                            // 參與者處理（已驗證有效，細項已被追蹤，EF Core 可正確排序 INSERT）
                            if (addDto.ParticipantIds != null)
                            {
                                // 收集有效的參與者 ID（已通過驗證）
                                var validItemParticipantIds = addDto.ParticipantIds
                                    .Select(pid => ResolveMemberId(pid)!.Value)
                                    .ToList();

                                if (validItemParticipantIds.Count > 0)
                                {
                                    // 使用 Penny Allocation 分配細項金額
                                    var itemAmounts = MoneyHelper.Allocate(item.Amount, validItemParticipantIds.Count);

                                    for (var i = 0; i < validItemParticipantIds.Count; i++)
                                    {
                                        item.Participants.Add(new ExpenseItemParticipant
                                        {
                                            ExpenseItemId = item.Id, // 明確設定 FK
                                            ExpenseItem = item,
                                            MemberId = validItemParticipantIds[i],
                                            Amount = itemAmounts[i]
                                        });
                                    }
                                }
                            }
                        }

                    // 4.2 更新細項
                    if (request.ExpenseItems.Update != null)
                        foreach (var updateDto in request.ExpenseItems.Update)
                        {
                            var item = bill.Expenses.SelectMany(e => e.Items)
                                .FirstOrDefault(i => i.Id == updateDto.RemoteId);
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

                            // 驗證 PaidByMemberId（幽靈參照檢查 - 無論版本衝突與否都要檢查）
                            if (!string.IsNullOrEmpty(updateDto.PaidByMemberId) && ResolveMemberId(updateDto.PaidByMemberId) == null)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"細項更新的付款者 ID '{updateDto.PaidByMemberId}' 無效或已刪除");

                            // 驗證 ParticipantIds（幽靈參照檢查 - 無論版本衝突與否都要檢查）
                            var invalidItemParticipantIds = ValidateParticipantIds(updateDto.ParticipantIds);
                            if (invalidItemParticipantIds.Count > 0)
                                return Result.Failure<DeltaSyncResponse>(
                                    "INVALID_MEMBER_REFERENCE",
                                    $"細項更新包含無效的參與者 ID: {string.Join(", ", invalidItemParticipantIds)}");

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

                                // 付款者處理（已驗證有效）
                                if (!string.IsNullOrEmpty(updateDto.PaidByMemberId))
                                    item.PaidById = ResolveMemberId(updateDto.PaidByMemberId);

                                if (updateDto.ParticipantIds != null)
                                {
                                    // 收集有效的參與者 ID（已通過驗證，保持順序以配合 Penny Allocation）
                                    var newMemberIds = updateDto.ParticipantIds
                                        .Select(pid => ResolveMemberId(pid)!.Value)
                                        .ToList();

                                    // 清除現有參與者並重新計算金額
                                    item.Participants.Clear();
                                    if (newMemberIds.Count > 0)
                                    {
                                        // 使用 Penny Allocation 分配細項金額
                                        var itemAmounts = MoneyHelper.Allocate(item.Amount, newMemberIds.Count);

                                        for (var i = 0; i < newMemberIds.Count; i++)
                                        {
                                            item.Participants.Add(new ExpenseItemParticipant
                                            {
                                                ExpenseItemId = item.Id,
                                                ExpenseItem = item,
                                                MemberId = newMemberIds[i],
                                                Amount = itemAmounts[i]
                                            });
                                        }
                                    }
                                }
                            }
                        }

                    // 4.3 刪除細項
                    if (request.ExpenseItems.Delete != null)
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

                // 5. 處理結算變更
                if (request.Settlements != null)
                {
                    // 處理 Mark (新增 SettledTransfer)
                    if (request.Settlements.Mark != null)
                        foreach (var sDto in request.Settlements.Mark)
                        {
                            Guid? fromId = memberIdMappings.TryGetValue(sDto.FromMemberId, out var fMapped) ? fMapped :
                                Guid.TryParse(sDto.FromMemberId, out var fGuid) ? fGuid : null;
                            Guid? toId = memberIdMappings.TryGetValue(sDto.ToMemberId, out var tMapped) ? tMapped :
                                Guid.TryParse(sDto.ToMemberId, out var tGuid) ? tGuid : null;

                            if (fromId.HasValue && toId.HasValue)
                                if (!bill.SettledTransfers.Any(st =>
                                        st.FromMemberId == fromId && st.ToMemberId == toId))
                                    bill.SettledTransfers.Add(new SettledTransfer
                                    {
                                        BillId = bill.Id,
                                        FromMemberId = fromId.Value,
                                        ToMemberId = toId.Value,
                                        Amount = sDto.Amount,
                                        SettledAt = DateTime.UtcNow
                                    });
                        }

                    // 處理 Unmark (刪除 SettledTransfer)
                    if (request.Settlements.Unmark != null)
                        foreach (var sDto in request.Settlements.Unmark)
                        {
                            Guid? fromId = Guid.TryParse(sDto.FromMemberId, out var fGuid) ? fGuid : null;
                            Guid? toId = Guid.TryParse(sDto.ToMemberId, out var tGuid) ? tGuid : null;

                            if (fromId.HasValue && toId.HasValue)
                            {
                                var existing = bill.SettledTransfers.FirstOrDefault(st =>
                                    st.FromMemberId == fromId && st.ToMemberId == toId);
                                if (existing != null) bill.SettledTransfers.Remove(existing);
                            }
                        }
                }

                // 6. 更新版本與儲存
                bill.Version++;
                bill.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(ct);

                // 儲存通知參數（在 transaction commit 後發送）
                notifyBillId = bill.Id;
                notifyVersion = bill.Version;

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
                    MergedBill = conflicts.Count > 0 || needsCarefulMerge ? MapToBillDto(bill) : null
                };
            }, ct);

            // Transaction 已成功 commit，現在發送 SignalR 通知
            if (notifyBillId.HasValue)
            {
                await _notificationService.NotifyBillUpdatedAsync(notifyBillId.Value, notifyVersion, userId);
            }

            return result;
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

    #endregion

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
            )).ToList(),
            bill.SettledTransfers.Select(st => st.ToKeyString()).ToList()
        );
    }
}
