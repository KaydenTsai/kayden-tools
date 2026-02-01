using Kayden.Commons.Common;
using Kayden.Commons.Extensions;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.SnapSplit;

public class BillService(
    IUnitOfWork unitOfWork,
    IBillNotificationService notificationService,
    IDateTimeService dateTimeService) : IBillService
{
    private readonly IBillNotificationService _notificationService = notificationService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDateTimeService _dateTimeService = dateTimeService;

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
        bill.UpdatedAt = _dateTimeService.UtcNow;
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
                    var existingBill = await _unitOfWork.Bills.GetByIdWithLockAsync(dto.RemoteId.Value, ct);
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
                            _dateTimeService.UtcNow,
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

                // 處理成員 Upsert / Delete
                SyncProcessMembers(bill, dto.Members, hasVersionConflict, memberIdMappings);

                // 處理費用 Upsert / Delete（含細項）
                await SyncProcessExpenses(bill, dto.Expenses, hasVersionConflict,
                    memberIdMappings, expenseIdMappings, expenseItemIdMappings, ct);

                // 處理已結清轉帳
                SyncProcessSettlements(bill, dto.SettledTransfers, memberIdMappings);

                // 更新版本號
                bill.Version++;
                bill.UpdatedAt = _dateTimeService.UtcNow;

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
                    _dateTimeService.UtcNow,
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
                        _dateTimeService.UtcNow,
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
                        _dateTimeService.UtcNow,
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
    /// SyncBill 子流程：處理成員 Upsert / Delete
    /// </summary>
    private void SyncProcessMembers(
        Bill bill,
        SyncMemberCollectionDto members,
        bool hasVersionConflict,
        Dictionary<string, Guid> memberIdMappings)
    {
        // 建立現有成員的 RemoteId 映射
        // 注意：使用 DistinctBy 避免 EF Core 關係修復導致的重複實體
        var existingMembersByRemoteId = bill.Members
            .DistinctBy(m => m.Id)
            .ToDictionary(m => m.Id);

        // 建立 LocalId -> RemoteId 映射表（從 Upsert 中）
        var memberLocalToRemoteMap = members.Upsert
            .Where(m => m.RemoteId.HasValue)
            .ToDictionary(m => m.LocalId, m => m.RemoteId!.Value);

        // 處理成員刪除 - 版本衝突時跳過刪除操作
        if (!hasVersionConflict)
        {
            foreach (var deletedId in members.DeletedIds)
            {
                Guid? remoteIdToDelete = null;

                if (Guid.TryParse(deletedId, out var parsedGuid))
                    remoteIdToDelete = parsedGuid;
                else if (memberLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                    remoteIdToDelete = mappedRemoteId;

                if (remoteIdToDelete.HasValue &&
                    existingMembersByRemoteId.TryGetValue(remoteIdToDelete.Value, out var memberToRemove))
                    bill.Members.Remove(memberToRemove);
            }
        }

        // 處理成員 Upsert
        foreach (var memberDto in members.Upsert)
        {
            if (members.DeletedIds.Contains(memberDto.LocalId))
                continue;

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
                    member.UpdatedAt = _dateTimeService.UtcNow;
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
    }

    /// <summary>
    /// SyncBill 子流程：處理費用 Upsert / Delete（含細項）
    /// </summary>
    private async Task SyncProcessExpenses(
        Bill bill,
        SyncExpenseCollectionDto expenses,
        bool hasVersionConflict,
        Dictionary<string, Guid> memberIdMappings,
        Dictionary<string, Guid> expenseIdMappings,
        Dictionary<string, Guid> expenseItemIdMappings,
        CancellationToken ct)
    {
        // 建立現有費用的 RemoteId 映射
        var existingExpensesByRemoteId = bill.Expenses
            .DistinctBy(e => e.Id)
            .ToDictionary(e => e.Id);

        // 建立 LocalId -> RemoteId 映射表
        var expenseLocalToRemoteMap = expenses.Upsert
            .Where(e => e.RemoteId.HasValue)
            .ToDictionary(e => e.LocalId, e => e.RemoteId!.Value);

        // 處理費用刪除 - 版本衝突時跳過刪除操作
        if (!hasVersionConflict)
        {
            foreach (var deletedId in expenses.DeletedIds)
            {
                Guid? remoteIdToDelete = null;

                if (Guid.TryParse(deletedId, out var parsedGuid))
                    remoteIdToDelete = parsedGuid;
                else if (expenseLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                    remoteIdToDelete = mappedRemoteId;

                if (remoteIdToDelete.HasValue &&
                    existingExpensesByRemoteId.TryGetValue(remoteIdToDelete.Value, out var expenseToRemove))
                    bill.Expenses.Remove(expenseToRemove);
            }
        }

        // 處理費用 Upsert
        foreach (var expenseDto in expenses.Upsert)
        {
            if (expenses.DeletedIds.Contains(expenseDto.LocalId))
                continue;

            Expense? expense = null;
            var isExistingExpense = expenseDto.RemoteId.HasValue &&
                existingExpensesByRemoteId.TryGetValue(expenseDto.RemoteId.Value, out expense);

            if (isExistingExpense && expense != null)
            {
                if (!hasVersionConflict)
                {
                    expense.Name = expenseDto.Name;
                    expense.Amount = expenseDto.Amount;
                    expense.ServiceFeePercent = expenseDto.ServiceFeePercent;
                    expense.IsItemized = expenseDto.IsItemized;
                    expense.UpdatedAt = _dateTimeService.UtcNow;
                }
            }
            else
            {
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
            }

            // 處理付款者和參與者（版本衝突時只處理新增的費用）
            if (!hasVersionConflict || !isExistingExpense)
            {
                if (!string.IsNullOrEmpty(expenseDto.PaidByLocalId) &&
                    memberIdMappings.TryGetValue(expenseDto.PaidByLocalId, out var paidById))
                    expense!.PaidById = paidById;

                var validParticipantIds = expenseDto.ParticipantLocalIds
                    .Where(lid => memberIdMappings.ContainsKey(lid))
                    .Select(lid => memberIdMappings[lid])
                    .ToList();
                SetExpenseParticipants(expense!, validParticipantIds);
            }

            // 處理費用細項
            if (expenseDto.IsItemized && expenseDto.Items != null)
                SyncProcessExpenseItems(expense!, expenseDto.Items, hasVersionConflict,
                    memberIdMappings, expenseItemIdMappings);

            expenseIdMappings[expenseDto.LocalId] = expense!.Id;
        }
    }

    /// <summary>
    /// SyncBill 子流程：處理費用細項 Upsert / Delete
    /// </summary>
    private void SyncProcessExpenseItems(
        Expense expense,
        SyncExpenseItemCollectionDto items,
        bool hasVersionConflict,
        Dictionary<string, Guid> memberIdMappings,
        Dictionary<string, Guid> expenseItemIdMappings)
    {
        var existingItemsByRemoteId = expense.Items
            .DistinctBy(i => i.Id)
            .ToDictionary(i => i.Id);

        var itemLocalToRemoteMap = items.Upsert
            .Where(i => i.RemoteId.HasValue)
            .ToDictionary(i => i.LocalId, i => i.RemoteId!.Value);

        // 處理細項刪除
        if (!hasVersionConflict)
        {
            foreach (var deletedId in items.DeletedIds)
            {
                Guid? remoteIdToDelete = null;

                if (Guid.TryParse(deletedId, out var parsedGuid))
                    remoteIdToDelete = parsedGuid;
                else if (itemLocalToRemoteMap.TryGetValue(deletedId, out var mappedRemoteId))
                    remoteIdToDelete = mappedRemoteId;

                if (remoteIdToDelete.HasValue &&
                    existingItemsByRemoteId.TryGetValue(remoteIdToDelete.Value, out var itemToRemove))
                    expense.Items.Remove(itemToRemove);
            }
        }

        // 處理細項 Upsert
        foreach (var itemDto in items.Upsert)
        {
            if (items.DeletedIds.Contains(itemDto.LocalId))
                continue;

            ExpenseItem? item = null;
            var isExistingItem = itemDto.RemoteId.HasValue &&
                existingItemsByRemoteId.TryGetValue(itemDto.RemoteId.Value, out item);

            if (isExistingItem && item != null)
            {
                if (!hasVersionConflict)
                {
                    item.Name = itemDto.Name;
                    item.Amount = itemDto.Amount;
                }
            }
            else
            {
                item = new ExpenseItem
                {
                    Id = Guid.NewGuid(),
                    ExpenseId = expense.Id,
                    Expense = expense,
                    Name = itemDto.Name,
                    Amount = itemDto.Amount
                };
                expense.Items.Add(item);
            }

            // 處理付款者和參與者（版本衝突時只處理新增的細項）
            if (!hasVersionConflict || !isExistingItem)
            {
                if (!string.IsNullOrEmpty(itemDto.PaidByLocalId) &&
                    memberIdMappings.TryGetValue(itemDto.PaidByLocalId, out var itemPaidById))
                    item!.PaidById = itemPaidById;

                var validItemParticipantIds = itemDto.ParticipantLocalIds
                    .Where(lid => memberIdMappings.ContainsKey(lid))
                    .Select(lid => memberIdMappings[lid])
                    .ToList();
                SetExpenseItemParticipants(item!, validItemParticipantIds);
            }

            expenseItemIdMappings[itemDto.LocalId] = item!.Id;
        }
    }

    /// <summary>
    /// SyncBill 子流程：處理已結清轉帳字串解析
    /// </summary>
    private void SyncProcessSettlements(
        Bill bill,
        List<string>? settledTransfers,
        Dictionary<string, Guid> memberIdMappings)
    {
        if (settledTransfers == null) return;

        bill.SettledTransfers.Clear();
        foreach (var transfer in settledTransfers)
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

            // 解析 fromId 和 toId（格式: "fromId::toId"，與 SettledTransfer.KeySeparator 一致）
            var separatorIndex = mainPart.IndexOf(SettledTransfer.KeySeparator, StringComparison.Ordinal);
            if (separatorIndex > 0 && separatorIndex < mainPart.Length - SettledTransfer.KeySeparator.Length)
            {
                var fromPart = mainPart.Substring(0, separatorIndex);
                var toPart = mainPart.Substring(separatorIndex + SettledTransfer.KeySeparator.Length);

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
                        SettledAt = _dateTimeService.UtcNow
                    });
            }
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
        // 用於儲存通知參數（在 transaction commit 後發送）
        Guid? notifyBillId = null;
        long notifyVersion = 0;

        try
        {
            var result = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var bill = await _unitOfWork.Bills.GetByIdWithLockAsync(billId, ct);
                if (bill == null) return Result.Failure<DeltaSyncResponse>(ErrorCodes.BillNotFound, "Bill not found.");

                var conflicts = new List<ConflictInfo>();
                var memberIdMappings = new Dictionary<string, Guid>();
                var expenseIdMappings = new Dictionary<string, Guid>();
                var expenseItemIdMappings = new Dictionary<string, Guid>();

                // 1. 版本檢查
                var needsCarefulMerge = request.BaseVersion < bill.Version;

                // 2. 處理成員變更
                if (request.Members != null)
                    await DeltaProcessMembers(bill, request.Members, needsCarefulMerge, conflicts, memberIdMappings);

                // 建立有效成員 ID 集合（用於驗證幽靈參照）
                var validMemberIds = bill.Members.Select(m => m.Id).ToHashSet();

                // 3. 處理費用變更
                if (request.Expenses != null)
                {
                    var expenseError = await DeltaProcessExpenses(bill, request.Expenses, needsCarefulMerge,
                        conflicts, memberIdMappings, validMemberIds, expenseIdMappings);
                    if (expenseError is { } err1) return err1;
                }

                // 4. 處理費用細項
                if (request.ExpenseItems != null)
                {
                    var itemError = await DeltaProcessItems(bill, request.ExpenseItems, needsCarefulMerge,
                        conflicts, memberIdMappings, validMemberIds, expenseIdMappings, expenseItemIdMappings);
                    if (itemError is { } err2) return err2;
                }

                // 5. 處理結算變更
                if (request.Settlements != null)
                    DeltaProcessSettlements(bill, request.Settlements, memberIdMappings);

                // 6. 更新版本與儲存
                bill.Version++;
                bill.UpdatedAt = _dateTimeService.UtcNow;
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

    #region DeltaSync 子流程

    /// <summary>
    /// 解析成員 ID：先查 LocalId 映射，再嘗試解析為已存在的 RemoteId
    /// </summary>
    private static Guid? ResolveDeltaMemberId(
        string? idString,
        Dictionary<string, Guid> memberIdMappings,
        HashSet<Guid> validMemberIds)
    {
        if (string.IsNullOrEmpty(idString)) return null;
        if (memberIdMappings.TryGetValue(idString, out var mappedId)) return mappedId;
        if (Guid.TryParse(idString, out var remoteId) && validMemberIds.Contains(remoteId)) return remoteId;
        return null;
    }

    /// <summary>
    /// 驗證一組成員 ID，回傳無效的 ID 列表
    /// </summary>
    private static List<string> ValidateDeltaParticipantIds(
        IEnumerable<string>? participantIds,
        Dictionary<string, Guid> memberIdMappings,
        HashSet<Guid> validMemberIds)
    {
        if (participantIds == null) return new List<string>();
        return participantIds
            .Where(pid => ResolveDeltaMemberId(pid, memberIdMappings, validMemberIds) == null)
            .ToList();
    }

    /// <summary>
    /// DeltaSync 子流程：處理成員 Add / Update / Delete
    /// </summary>
    private async Task DeltaProcessMembers(
        Bill bill,
        MemberChangesDto members,
        bool needsCarefulMerge,
        List<ConflictInfo> conflicts,
        Dictionary<string, Guid> memberIdMappings)
    {
        // 處理新增成員
        if (members.Add != null)
            foreach (var addDto in members.Add)
            {
                if (memberIdMappings.ContainsKey(addDto.LocalId))
                    continue;

                var existingMember = bill.Members.FirstOrDefault(m => m.LocalClientId == addDto.LocalId);
                if (existingMember != null)
                {
                    memberIdMappings[addDto.LocalId] = existingMember.Id;
                    continue;
                }

                var member = new Member
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    LocalClientId = addDto.LocalId,
                    Name = addDto.Name,
                    DisplayOrder = addDto.DisplayOrder ??
                                   (bill.Members.Count > 0 ? bill.Members.Max(m => m.DisplayOrder) + 1 : 0),
                    LinkedUserId = addDto.LinkedUserId,
                    ClaimedAt = addDto.ClaimedAt
                };
                await _unitOfWork.Members.AddAsync(member);
                bill.Members.Add(member);
                memberIdMappings[addDto.LocalId] = member.Id;
            }

        // 處理更新成員
        if (members.Update != null)
            foreach (var updateDto in members.Update)
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

                    if (updateDto.LinkedUserId.HasValue) member.LinkedUserId = updateDto.LinkedUserId;
                    if (updateDto.ClaimedAt.HasValue) member.ClaimedAt = updateDto.ClaimedAt;
                }
                else
                {
                    if (updateDto.Name != null) member.Name = updateDto.Name;
                    if (updateDto.DisplayOrder.HasValue) member.DisplayOrder = updateDto.DisplayOrder.Value;
                    if (updateDto.LinkedUserId.HasValue) member.LinkedUserId = updateDto.LinkedUserId;
                    if (updateDto.ClaimedAt.HasValue) member.ClaimedAt = updateDto.ClaimedAt;
                    member.UpdatedAt = _dateTimeService.UtcNow;
                }
            }

        // 處理刪除成員
        if (members.Delete != null)
            foreach (var remoteId in members.Delete)
            {
                var member = bill.Members.FirstOrDefault(m => m.Id == remoteId);
                if (member == null) continue;

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

    /// <summary>
    /// DeltaSync 子流程：處理費用 Add / Update / Delete
    /// </summary>
    /// <returns>驗證失敗時回傳錯誤結果，成功時回傳 null</returns>
    private async Task<Result<DeltaSyncResponse>?> DeltaProcessExpenses(
        Bill bill,
        ExpenseChangesDto expenses,
        bool needsCarefulMerge,
        List<ConflictInfo> conflicts,
        Dictionary<string, Guid> memberIdMappings,
        HashSet<Guid> validMemberIds,
        Dictionary<string, Guid> expenseIdMappings)
    {
        // 新增費用
        if (expenses.Add != null)
            foreach (var addDto in expenses.Add)
            {
                if (expenseIdMappings.ContainsKey(addDto.LocalId))
                    continue;

                var existingExpense = bill.Expenses.FirstOrDefault(e => e.LocalClientId == addDto.LocalId);
                if (existingExpense != null)
                {
                    expenseIdMappings[addDto.LocalId] = existingExpense.Id;
                    continue;
                }

                // 驗證幽靈參照
                if (!string.IsNullOrEmpty(addDto.PaidByMemberId) &&
                    ResolveDeltaMemberId(addDto.PaidByMemberId, memberIdMappings, validMemberIds) == null)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"費用 '{addDto.Name}' 的付款者 ID '{addDto.PaidByMemberId}' 無效或已刪除");

                var invalidIds = ValidateDeltaParticipantIds(addDto.ParticipantIds, memberIdMappings, validMemberIds);
                if (invalidIds.Count > 0)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"費用 '{addDto.Name}' 包含無效的參與者 ID: {string.Join(", ", invalidIds)}");

                var expense = new Expense
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    LocalClientId = addDto.LocalId,
                    Name = addDto.Name,
                    Amount = addDto.Amount,
                    ServiceFeePercent = addDto.ServiceFeePercent ?? 0,
                    IsItemized = addDto.IsItemized ?? false
                };

                expense.PaidById = ResolveDeltaMemberId(addDto.PaidByMemberId, memberIdMappings, validMemberIds);

                await _unitOfWork.Expenses.AddAsync(expense);
                bill.Expenses.Add(expense);
                expenseIdMappings[addDto.LocalId] = expense.Id;

                if (addDto.ParticipantIds != null)
                {
                    var participantIds = addDto.ParticipantIds
                        .Select(pid => ResolveDeltaMemberId(pid, memberIdMappings, validMemberIds)!.Value)
                        .ToList();
                    SetExpenseParticipants(expense, participantIds);
                }
            }

        // 更新費用
        if (expenses.Update != null)
            foreach (var updateDto in expenses.Update)
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

                // 驗證幽靈參照
                if (!string.IsNullOrEmpty(updateDto.PaidByMemberId) &&
                    ResolveDeltaMemberId(updateDto.PaidByMemberId, memberIdMappings, validMemberIds) == null)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"費用更新的付款者 ID '{updateDto.PaidByMemberId}' 無效或已刪除");

                var invalidIds = ValidateDeltaParticipantIds(updateDto.ParticipantIds, memberIdMappings, validMemberIds);
                if (invalidIds.Count > 0)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"費用更新包含無效的參與者 ID: {string.Join(", ", invalidIds)}");

                if (needsCarefulMerge)
                {
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

                    if (!string.IsNullOrEmpty(updateDto.PaidByMemberId))
                        expense.PaidById = ResolveDeltaMemberId(updateDto.PaidByMemberId, memberIdMappings, validMemberIds);

                    if (updateDto.ParticipantIds != null)
                    {
                        var newMemberIds = updateDto.ParticipantIds
                            .Select(pid => ResolveDeltaMemberId(pid, memberIdMappings, validMemberIds)!.Value)
                            .ToList();
                        SetExpenseParticipants(expense, newMemberIds);
                    }

                    expense.UpdatedAt = _dateTimeService.UtcNow;
                }
            }

        // 刪除費用
        if (expenses.Delete != null)
            foreach (var remoteId in expenses.Delete)
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

        return null; // 無錯誤
    }

    /// <summary>
    /// DeltaSync 子流程：處理費用細項 Add / Update / Delete
    /// </summary>
    /// <returns>驗證失敗時回傳錯誤結果，成功時回傳 null</returns>
    private async Task<Result<DeltaSyncResponse>?> DeltaProcessItems(
        Bill bill,
        ExpenseItemChangesDto expenseItems,
        bool needsCarefulMerge,
        List<ConflictInfo> conflicts,
        Dictionary<string, Guid> memberIdMappings,
        HashSet<Guid> validMemberIds,
        Dictionary<string, Guid> expenseIdMappings,
        Dictionary<string, Guid> expenseItemIdMappings)
    {
        // 新增細項
        if (expenseItems.Add != null)
            foreach (var addDto in expenseItems.Add)
            {
                if (expenseItemIdMappings.ContainsKey(addDto.LocalId))
                    continue;

                // 解析 ExpenseId
                Guid expenseGuid;
                if (expenseIdMappings.TryGetValue(addDto.ExpenseId, out var mappedExpenseId))
                    expenseGuid = mappedExpenseId;
                else if (!Guid.TryParse(addDto.ExpenseId, out expenseGuid))
                    continue;

                var expense = bill.Expenses.FirstOrDefault(e => e.Id == expenseGuid);
                if (expense == null) continue;

                var existingItem = expense.Items.FirstOrDefault(i => i.LocalClientId == addDto.LocalId);
                if (existingItem != null)
                {
                    expenseItemIdMappings[addDto.LocalId] = existingItem.Id;
                    continue;
                }

                // 驗證幽靈參照
                if (!string.IsNullOrEmpty(addDto.PaidByMemberId) &&
                    ResolveDeltaMemberId(addDto.PaidByMemberId, memberIdMappings, validMemberIds) == null)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"細項 '{addDto.Name}' 的付款者 ID '{addDto.PaidByMemberId}' 無效或已刪除");

                var invalidIds = ValidateDeltaParticipantIds(addDto.ParticipantIds, memberIdMappings, validMemberIds);
                if (invalidIds.Count > 0)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"細項 '{addDto.Name}' 包含無效的參與者 ID: {string.Join(", ", invalidIds)}");

                var item = new ExpenseItem
                {
                    Id = Guid.NewGuid(),
                    ExpenseId = expense.Id,
                    Expense = expense,
                    LocalClientId = addDto.LocalId,
                    Name = addDto.Name,
                    Amount = addDto.Amount
                };

                item.PaidById = ResolveDeltaMemberId(addDto.PaidByMemberId, memberIdMappings, validMemberIds);

                await _unitOfWork.ExpenseItems.AddAsync(item);
                expense.Items.Add(item);
                expenseItemIdMappings[addDto.LocalId] = item.Id;

                if (addDto.ParticipantIds != null)
                {
                    var participantIds = addDto.ParticipantIds
                        .Select(pid => ResolveDeltaMemberId(pid, memberIdMappings, validMemberIds)!.Value)
                        .ToList();
                    SetExpenseItemParticipants(item, participantIds);
                }
            }

        // 更新細項
        if (expenseItems.Update != null)
            foreach (var updateDto in expenseItems.Update)
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

                // 驗證幽靈參照
                if (!string.IsNullOrEmpty(updateDto.PaidByMemberId) &&
                    ResolveDeltaMemberId(updateDto.PaidByMemberId, memberIdMappings, validMemberIds) == null)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"細項更新的付款者 ID '{updateDto.PaidByMemberId}' 無效或已刪除");

                var invalidIds = ValidateDeltaParticipantIds(updateDto.ParticipantIds, memberIdMappings, validMemberIds);
                if (invalidIds.Count > 0)
                    return Result.Failure<DeltaSyncResponse>(
                        "INVALID_MEMBER_REFERENCE",
                        $"細項更新包含無效的參與者 ID: {string.Join(", ", invalidIds)}");

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
                        item.PaidById = ResolveDeltaMemberId(updateDto.PaidByMemberId, memberIdMappings, validMemberIds);

                    if (updateDto.ParticipantIds != null)
                    {
                        var newMemberIds = updateDto.ParticipantIds
                            .Select(pid => ResolveDeltaMemberId(pid, memberIdMappings, validMemberIds)!.Value)
                            .ToList();
                        SetExpenseItemParticipants(item, newMemberIds);
                    }
                }
            }

        // 刪除細項
        if (expenseItems.Delete != null)
            foreach (var remoteId in expenseItems.Delete)
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

        return null; // 無錯誤
    }

    /// <summary>
    /// DeltaSync 子流程：處理結算 Mark / Unmark
    /// </summary>
    private void DeltaProcessSettlements(
        Bill bill,
        SettlementChangesDto settlements,
        Dictionary<string, Guid> memberIdMappings)
    {
        // 處理 Mark (新增 SettledTransfer)
        if (settlements.Mark != null)
            foreach (var sDto in settlements.Mark)
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
                            SettledAt = _dateTimeService.UtcNow
                        });
            }

        // 處理 Unmark (刪除 SettledTransfer)
        if (settlements.Unmark != null)
            foreach (var sDto in settlements.Unmark)
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

    #endregion

    /// <summary>
    /// 設定費用參與者（使用 Penny Allocation 確保分攤精度）
    /// </summary>
    private static void SetExpenseParticipants(Expense expense, List<Guid> memberIds)
    {
        expense.Participants.Clear();
        if (memberIds.Count == 0) return;

        var totalWithFee = MoneyHelper.CalculateAmountWithServiceFee(
            expense.Amount, expense.ServiceFeePercent);
        var amounts = MoneyHelper.Allocate(totalWithFee, memberIds.Count);

        for (var i = 0; i < memberIds.Count; i++)
        {
            expense.Participants.Add(new ExpenseParticipant
            {
                ExpenseId = expense.Id,
                Expense = expense,
                MemberId = memberIds[i],
                Amount = amounts[i]
            });
        }
    }

    /// <summary>
    /// 設定細項參與者（使用 Penny Allocation 確保分攤精度）
    /// </summary>
    private static void SetExpenseItemParticipants(ExpenseItem item, List<Guid> memberIds)
    {
        item.Participants.Clear();
        if (memberIds.Count == 0) return;

        var amounts = MoneyHelper.Allocate(item.Amount, memberIds.Count);

        for (var i = 0; i < memberIds.Count; i++)
        {
            item.Participants.Add(new ExpenseItemParticipant
            {
                ExpenseItemId = item.Id,
                ExpenseItem = item,
                MemberId = memberIds[i],
                Amount = amounts[i]
            });
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
            )).ToList(),
            bill.SettledTransfers.Select(st => st.ToKeyString()).ToList()
        );
    }
}
