using System.Text.Json;
using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Services.SnapSplit;

public class OperationService : IOperationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeService _dateTimeService;

    public OperationService(IUnitOfWork unitOfWork, IDateTimeService dateTimeService)
    {
        _unitOfWork = unitOfWork;
        _dateTimeService = dateTimeService;
    }

    #region IOperationService Members

    public async Task<Result<OperationDto>> ProcessOperationAsync(OperationRequestDto request, Guid? userId,
        CancellationToken ct = default)
    {
        try
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 1. 取得帳單並使用 SELECT FOR UPDATE 鎖定該列
                // 這可以防止並發修改造成的競態條件
                var bill = await _unitOfWork.Bills.GetByIdWithLockAsync(request.BillId, ct);
                if (bill == null) return Result.Failure<OperationDto>(ErrorCodes.BillNotFound, "Bill not found.");

                // 樂觀鎖檢查：版本不符表示需要 Rebase
                if (request.BaseVersion != bill.Version)
                {
                    // 回傳衝突並附帶遺漏的操作
                    var missingOps = await GetOperationsAsync(request.BillId, request.BaseVersion, ct);
                    return Result.Failure<OperationDto>(
                        ErrorCodes.Conflict,
                        $"Version mismatch. Current: {bill.Version}, Base: {request.BaseVersion}. Missing {missingOps.Count} operations.");
                }

                // 2. 建立 Operation 紀錄
                var nextVersion = bill.Version + 1;

                // 將 JsonElement 轉換為 JsonDocument
                var payloadDocument = JsonDocument.Parse(request.Payload.GetRawText());

                var operation = new Operation
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    Version = nextVersion,
                    OpType = request.OpType,
                    TargetId = request.TargetId,
                    Payload = payloadDocument,
                    CreatedByUserId = userId,
                    ClientId = request.ClientId,
                    CreatedAt = _dateTimeService.UtcNow
                };

                // 3. 套用變更到 Snapshot (Read Models)
                // 如果拋出例外，ExecuteInTransactionAsync 會自動 rollback
                ApplyOperationToSnapshot(bill, operation);

                // 4. 更新帳單版本
                bill.Version = nextVersion;
                bill.UpdatedAt = _dateTimeService.UtcNow;

                // 5. 儲存到資料庫（在同一交易中）
                // bill 是從資料庫載入的，EF Core 會自動追蹤變更，不需要呼叫 Update()
                await _unitOfWork.Operations.AddAsync(operation, ct);
                await _unitOfWork.SaveChangesAsync(ct);

                return MapToDto(operation);
            }, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // 資料庫層級的並發衝突：另一個事務在我們讀取和寫入之間修改了 Bill
            // 清除追蹤狀態以避免後續操作受影響
            _unitOfWork.ClearChangeTracker();

            // 回傳衝突錯誤，讓前端進行 Rebase
            var missingOps = await GetOperationsAsync(request.BillId, request.BaseVersion, ct);
            return Result.Failure<OperationDto>(
                ErrorCodes.Conflict,
                $"Concurrent modification detected. Please retry with updated version. Missing {missingOps.Count} operations.");
        }
    }

    public async Task<List<OperationDto>> GetOperationsAsync(Guid billId, long sinceVersion,
        CancellationToken ct = default)
    {
        var ops = await _unitOfWork.Operations.FindAsync(
            x => x.BillId == billId && x.Version > sinceVersion,
            q => q.OrderBy(x => x.Version),
            ct: ct);

        return ops.Select(MapToDto).ToList();
    }

    #endregion

    private void ApplyOperationToSnapshot(Bill bill, Operation op)
    {
        var payload = op.Payload.RootElement;

        switch (op.OpType)
        {
            case "BILL_UPDATE_META":
                if (payload.TryGetProperty("name", out var name)) bill.Name = name.GetString() ?? bill.Name;
                break;

            case "MEMBER_ADD":
                bill.Members.Add(new Member
                {
                    Id = op.TargetId ?? Guid.NewGuid(),
                    BillId = bill.Id,
                    Name = payload.GetProperty("name").GetString() ?? "New Member",
                    DisplayOrder = payload.TryGetProperty("displayOrder", out var order) ? order.GetInt32() : 0
                });
                break;

            case "MEMBER_UPDATE":
                var member = bill.Members.FirstOrDefault(m => m.Id == op.TargetId);
                if (member != null)
                {
                    if (payload.TryGetProperty("name", out var mName)) member.Name = mName.GetString() ?? member.Name;
                    if (payload.TryGetProperty("displayOrder", out var mOrder)) member.DisplayOrder = mOrder.GetInt32();
                    member.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "MEMBER_CLAIM":
                var memberToClaim = bill.Members.FirstOrDefault(m => m.Id == op.TargetId);
                if (memberToClaim != null && op.CreatedByUserId.HasValue)
                {
                    memberToClaim.OriginalName = memberToClaim.Name;
                    memberToClaim.LinkedUserId = op.CreatedByUserId;
                    memberToClaim.ClaimedAt = _dateTimeService.UtcNow;
                    memberToClaim.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "MEMBER_UNCLAIM":
                var memberToUnclaim = bill.Members.FirstOrDefault(m => m.Id == op.TargetId);
                if (memberToUnclaim != null)
                {
                    if (memberToUnclaim.OriginalName != null) memberToUnclaim.Name = memberToUnclaim.OriginalName;
                    memberToUnclaim.OriginalName = null;
                    memberToUnclaim.LinkedUserId = null;
                    memberToUnclaim.ClaimedAt = null;
                    memberToUnclaim.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "MEMBER_REORDER":
                if (payload.TryGetProperty("order", out var orderArray))
                {
                    var orderList = orderArray.EnumerateArray()
                        .Select(x => x.GetGuid())
                        .ToList();
                    for (var i = 0; i < orderList.Count; i++)
                    {
                        var m = bill.Members.FirstOrDefault(x => x.Id == orderList[i]);
                        if (m != null)
                        {
                            m.DisplayOrder = i;
                            m.UpdatedAt = _dateTimeService.UtcNow;
                        }
                    }
                }

                break;

            case "MEMBER_REMOVE":
                var memberToRemove = bill.Members.FirstOrDefault(m => m.Id == op.TargetId);
                if (memberToRemove != null) bill.Members.Remove(memberToRemove);
                break;

            case "EXPENSE_ADD":
                var expense = new Expense
                {
                    Id = op.TargetId ?? Guid.NewGuid(),
                    BillId = bill.Id,
                    Name = payload.GetProperty("name").GetString() ?? "New Expense",
                    Amount = payload.GetProperty("amount").GetDecimal(),
                    ServiceFeePercent = payload.TryGetProperty("serviceFeePercent", out var fee) ? fee.GetDecimal() : 0,
                    PaidById = payload.TryGetProperty("paidById", out var pId) && pId.ValueKind != JsonValueKind.Null
                        ? pId.GetGuid()
                        : null
                };
                bill.Expenses.Add(expense);
                break;

            case "EXPENSE_UPDATE":
                var exp = bill.Expenses.FirstOrDefault(e => e.Id == op.TargetId);
                if (exp != null)
                {
                    if (payload.TryGetProperty("name", out var eName)) exp.Name = eName.GetString() ?? exp.Name;
                    if (payload.TryGetProperty("amount", out var eAmount)) exp.Amount = eAmount.GetDecimal();
                    if (payload.TryGetProperty("paidById", out var ePaidById))
                        exp.PaidById = ePaidById.ValueKind == JsonValueKind.Null ? null : ePaidById.GetGuid();
                }

                break;

            case "EXPENSE_DELETE":
                var expToDelete = bill.Expenses.FirstOrDefault(e => e.Id == op.TargetId);
                if (expToDelete != null) bill.Expenses.Remove(expToDelete);
                break;

            case "EXPENSE_SET_PARTICIPANTS":
                var expForParticipants = bill.Expenses.FirstOrDefault(e => e.Id == op.TargetId);
                if (expForParticipants != null && payload.TryGetProperty("participantIds", out var participantIds))
                {
                    expForParticipants.Participants.Clear();
                    foreach (var pid in participantIds.EnumerateArray())
                        expForParticipants.Participants.Add(new ExpenseParticipant
                        {
                            ExpenseId = expForParticipants.Id,
                            MemberId = pid.GetGuid()
                        });
                    expForParticipants.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "EXPENSE_TOGGLE_ITEMIZED":
                var expForItemized = bill.Expenses.FirstOrDefault(e => e.Id == op.TargetId);
                if (expForItemized != null)
                {
                    expForItemized.IsItemized = !expForItemized.IsItemized;
                    expForItemized.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "EXPENSE_REORDER":
                // 備註：Expense 目前無 DisplayOrder 欄位，透過 CreatedAt 或前端排序處理
                // 未來如需排序可新增 DisplayOrder 欄位
                break;

            // Item 操作
            case "ITEM_ADD":
                var expenseForNewItem = bill.Expenses.FirstOrDefault(e =>
                    e.Id == (payload.TryGetProperty("expenseId", out var expId) ? expId.GetGuid() : Guid.Empty));
                if (expenseForNewItem != null)
                {
                    var newItem = new ExpenseItem
                    {
                        Id = op.TargetId ?? Guid.NewGuid(),
                        ExpenseId = expenseForNewItem.Id,
                        Name = payload.GetProperty("name").GetString() ?? "New Item",
                        Amount = payload.GetProperty("amount").GetDecimal(),
                        PaidById = payload.TryGetProperty("paidById", out var itemPaidById) &&
                                   itemPaidById.ValueKind != JsonValueKind.Null
                            ? itemPaidById.GetGuid()
                            : expenseForNewItem.PaidById
                    };
                    expenseForNewItem.Items.Add(newItem);
                    expenseForNewItem.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "ITEM_UPDATE":
                var itemToUpdate = FindItemById(bill, op.TargetId);
                if (itemToUpdate != null)
                {
                    if (payload.TryGetProperty("name", out var itemName))
                        itemToUpdate.Name = itemName.GetString() ?? itemToUpdate.Name;
                    if (payload.TryGetProperty("amount", out var itemAmount))
                        itemToUpdate.Amount = itemAmount.GetDecimal();
                    if (payload.TryGetProperty("paidById", out var newPaidById))
                        itemToUpdate.PaidById =
                            newPaidById.ValueKind == JsonValueKind.Null ? null : newPaidById.GetGuid();
                }

                break;

            case "ITEM_DELETE":
                var (itemToDelete, parentExpense) = FindItemAndParent(bill, op.TargetId);
                if (itemToDelete != null && parentExpense != null)
                {
                    parentExpense.Items.Remove(itemToDelete);
                    parentExpense.UpdatedAt = _dateTimeService.UtcNow;
                }

                break;

            case "ITEM_SET_PARTICIPANTS":
                var itemForParticipants = FindItemById(bill, op.TargetId);
                if (itemForParticipants != null && payload.TryGetProperty("participantIds", out var itemParticipantIds))
                {
                    itemForParticipants.Participants.Clear();
                    foreach (var pid in itemParticipantIds.EnumerateArray())
                        itemForParticipants.Participants.Add(new ExpenseItemParticipant
                        {
                            ExpenseItemId = itemForParticipants.Id,
                            MemberId = pid.GetGuid()
                        });
                }

                break;

            // Settlement 操作
            case "SETTLEMENT_MARK":
                if (payload.TryGetProperty("fromMemberId", out var sFromId) &&
                    payload.TryGetProperty("toMemberId", out var sToId))
                {
                    var fromMemberId = sFromId.GetGuid();
                    var toMemberId = sToId.GetGuid();
                    var amount = payload.TryGetProperty("amount", out var sAmount) ? sAmount.GetDecimal() : 0;

                    // 檢查是否已存在
                    var existing = bill.SettledTransfers.FirstOrDefault(s =>
                        s.FromMemberId == fromMemberId && s.ToMemberId == toMemberId);
                    if (existing == null)
                        bill.SettledTransfers.Add(new SettledTransfer
                        {
                            BillId = bill.Id,
                            FromMemberId = fromMemberId,
                            ToMemberId = toMemberId,
                            Amount = amount,
                            SettledAt = _dateTimeService.UtcNow
                        });
                }

                break;

            case "SETTLEMENT_UNMARK":
                if (payload.TryGetProperty("fromMemberId", out var uFromId) &&
                    payload.TryGetProperty("toMemberId", out var uToId))
                {
                    var existing = bill.SettledTransfers.FirstOrDefault(s =>
                        s.FromMemberId == uFromId.GetGuid() && s.ToMemberId == uToId.GetGuid());
                    if (existing != null) bill.SettledTransfers.Remove(existing);
                }

                break;

            case "SETTLEMENT_CLEAR_ALL":
                bill.SettledTransfers.Clear();
                break;
        }
    }

    private static ExpenseItem? FindItemById(Bill bill, Guid? itemId)
    {
        if (!itemId.HasValue) return null;
        return bill.Expenses.SelectMany(e => e.Items).FirstOrDefault(i => i.Id == itemId.Value);
    }

    private static (ExpenseItem? Item, Expense? Parent) FindItemAndParent(Bill bill, Guid? itemId)
    {
        if (!itemId.HasValue) return (null, null);
        foreach (var expense in bill.Expenses)
        {
            var item = expense.Items.FirstOrDefault(i => i.Id == itemId.Value);
            if (item != null) return (item, expense);
        }

        return (null, null);
    }

    private static OperationDto MapToDto(Operation op)
    {
        return new OperationDto(
            op.Id,
            op.BillId,
            op.Version,
            op.OpType,
            op.TargetId,
            op.Payload,
            op.CreatedByUserId,
            op.ClientId,
            op.CreatedAt
        );
    }
}
