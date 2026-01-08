using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Core.Common;
using KaydenTools.Models.SnapSplit.Dtos;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using KaydenTools.Services.Interfaces;

namespace KaydenTools.Services.SnapSplit;

/// <summary>
/// 結算管理服務實作
/// </summary>
public class SettlementService : ISettlementService
{
    private readonly IDateTimeService _dateTimeService;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// 建構子
    /// </summary>
    /// <param name="unitOfWork">工作單元</param>
    /// <param name="dateTimeService">日期時間服務</param>
    public SettlementService(IUnitOfWork unitOfWork, IDateTimeService dateTimeService)
    {
        _unitOfWork = unitOfWork;
        _dateTimeService = dateTimeService;
    }

    #region ISettlementService Members

    /// <summary>
    /// 計算帳單結算
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>結算結果</returns>
    public async Task<Result<SettlementResultDto>> CalculateAsync(Guid billId, CancellationToken ct = default)
    {
        var bill = await _unitOfWork.Bills.GetByIdWithDetailsAsync(billId, ct);
        if (bill == null) return Result.Failure<SettlementResultDto>(ErrorCodes.BillNotFound, "Bill not found.");

        // 取得已結清的轉帳記錄
        var settledTransfers = await _unitOfWork.SettledTransfers.GetByBillIdAsync(billId, ct);
        var settledSet = settledTransfers
            .Select(s => (s.FromMemberId, s.ToMemberId))
            .ToHashSet();

        // 計算結算
        var result = CalculateSettlement(bill, settledSet);

        return result;
    }

    /// <summary>
    /// 切換結清狀態
    /// </summary>
    /// <param name="billId">帳單 ID</param>
    /// <param name="fromMemberId">付款方成員 ID</param>
    /// <param name="toMemberId">收款方成員 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作結果</returns>
    public async Task<Result> ToggleSettledAsync(Guid billId, Guid fromMemberId, Guid toMemberId,
        CancellationToken ct = default)
    {
        // 檢查帳單是否存在
        var bill = await _unitOfWork.Bills.GetByIdAsync(billId, ct);
        if (bill == null) return Result.Failure(ErrorCodes.BillNotFound, "Bill not found.");

        // 檢查成員是否存在
        var fromMember = await _unitOfWork.Members.GetByIdAsync(fromMemberId, ct);
        var toMember = await _unitOfWork.Members.GetByIdAsync(toMemberId, ct);

        if (fromMember == null || fromMember.BillId != billId)
            return Result.Failure(ErrorCodes.MemberNotFound, "From member not found in this bill.");

        if (toMember == null || toMember.BillId != billId)
            return Result.Failure(ErrorCodes.MemberNotFound, "To member not found in this bill.");

        // 尋找現有的已結清記錄
        var settledTransfer = await _unitOfWork.SettledTransfers.GetByKeyAsync(billId, fromMemberId, toMemberId, ct);

        if (settledTransfer == null)
        {
            // 建立新的已結清記錄
            settledTransfer = new SettledTransfer
            {
                BillId = billId,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                Amount = 0, // 金額在 CalculateAsync 中動態計算
                SettledAt = _dateTimeService.UtcNow
            };

            await _unitOfWork.SettledTransfers.AddAsync(settledTransfer);
        }
        else
        {
            // 已存在則移除（取消結清）
            _unitOfWork.SettledTransfers.Remove(settledTransfer);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    #endregion

    /// <summary>
    /// 計算帳單的完整結算結果
    /// </summary>
    private static SettlementResultDto CalculateSettlement(Bill bill, HashSet<(Guid, Guid)> settledSet)
    {
        var members = bill.Members.ToList();
        var expenses = bill.Expenses.ToList();

        // 初始化成員摘要
        var summaryMap = members.ToDictionary(
            m => m.Id,
            m => new MemberBalanceInfo
            {
                MemberId = m.Id,
                MemberName = m.Name,
                TotalPaid = 0m,
                TotalOwed = 0m
            });

        var totalAmount = 0m;
        var totalWithServiceFee = 0m;

        // 處理每筆消費
        foreach (var expense in expenses)
            if (expense.IsItemized)
                ProcessItemizedExpense(expense, summaryMap, ref totalAmount, ref totalWithServiceFee);
            else
                ProcessSimpleExpense(expense, summaryMap, ref totalAmount, ref totalWithServiceFee);

        // 計算餘額並四捨五入
        var memberBalances = summaryMap.Values
            .Select(s => new MemberBalanceDto(
                s.MemberId,
                s.MemberName,
                Math.Round(s.TotalPaid, 2),
                Math.Round(s.TotalOwed, 2),
                Math.Round(s.TotalPaid - s.TotalOwed, 2)))
            .ToList();

        // 計算轉帳清單
        var transfers = CalculateTransfers(memberBalances, settledSet);

        return new SettlementResultDto(
            Math.Round(totalAmount, 2),
            Math.Round(totalWithServiceFee, 2),
            memberBalances,
            transfers);
    }

    /// <summary>
    /// 處理簡單模式消費
    /// </summary>
    private static void ProcessSimpleExpense(
        Expense expense,
        Dictionary<Guid, MemberBalanceInfo> summaryMap,
        ref decimal totalAmount,
        ref decimal totalWithServiceFee)
    {
        var participants = expense.Participants.ToList();
        if (participants.Count == 0 || !expense.PaidById.HasValue) return;

        var amountWithFee = ApplyServiceFee(expense.Amount, expense.ServiceFeePercent);
        var sharePerPerson = amountWithFee / participants.Count;

        totalAmount += expense.Amount;
        totalWithServiceFee += amountWithFee;

        // 付款人增加實付金額
        if (summaryMap.TryGetValue(expense.PaidById.Value, out var payer)) payer.TotalPaid += amountWithFee;

        // 參與者增加應付金額
        foreach (var participant in participants)
            if (summaryMap.TryGetValue(participant.MemberId, out var p))
                p.TotalOwed += sharePerPerson;
    }

    /// <summary>
    /// 處理品項模式消費
    /// </summary>
    private static void ProcessItemizedExpense(
        Expense expense,
        Dictionary<Guid, MemberBalanceInfo> summaryMap,
        ref decimal totalAmount,
        ref decimal totalWithServiceFee)
    {
        var items = expense.Items.ToList();
        if (items.Count == 0) return;

        foreach (var item in items)
        {
            var participants = item.Participants.ToList();
            if (participants.Count == 0) continue;

            var itemWithFee = ApplyServiceFee(item.Amount, expense.ServiceFeePercent);
            var sharePerPerson = itemWithFee / participants.Count;

            totalAmount += item.Amount;
            totalWithServiceFee += itemWithFee;

            // 付款人增加實付（如果有指定付款者）
            if (item.PaidById.HasValue && summaryMap.TryGetValue(item.PaidById.Value, out var payer))
                payer.TotalPaid += itemWithFee;

            // 參與者增加應付
            foreach (var participant in participants)
                if (summaryMap.TryGetValue(participant.MemberId, out var p))
                    p.TotalOwed += sharePerPerson;
        }
    }

    /// <summary>
    /// 計算含服務費的金額
    /// </summary>
    private static decimal ApplyServiceFee(decimal amount, decimal serviceFeePercent)
    {
        return amount * (1 + serviceFeePercent / 100);
    }

    /// <summary>
    /// 使用貪婪法產生最小化轉帳清單
    /// </summary>
    private static List<TransferDto> CalculateTransfers(
        List<MemberBalanceDto> memberBalances,
        HashSet<(Guid, Guid)> settledSet)
    {
        const decimal threshold = 0.01m;

        var creditors = memberBalances
            .Where(b => b.Balance > threshold)
            .Select(b => new { b.MemberId, b.MemberName, Amount = b.Balance })
            .OrderByDescending(c => c.Amount)
            .ToList();

        var debtors = memberBalances
            .Where(b => b.Balance < -threshold)
            .Select(b => new { b.MemberId, b.MemberName, Amount = Math.Abs(b.Balance) })
            .OrderByDescending(d => d.Amount)
            .ToList();

        // 使用可變動的清單來追蹤餘額
        var creditorAmounts = creditors.Select(c => c.Amount).ToList();
        var debtorAmounts = debtors.Select(d => d.Amount).ToList();

        var transfers = new List<TransferDto>();
        int ci = 0, di = 0;

        while (ci < creditors.Count && di < debtors.Count)
        {
            var creditor = creditors[ci];
            var debtor = debtors[di];
            var transferAmount = Math.Min(creditorAmounts[ci], debtorAmounts[di]);

            if (transferAmount > threshold)
            {
                var isSettled = settledSet.Contains((debtor.MemberId, creditor.MemberId));

                transfers.Add(new TransferDto(
                    debtor.MemberId,
                    debtor.MemberName,
                    creditor.MemberId,
                    creditor.MemberName,
                    Math.Round(transferAmount, 2),
                    isSettled));
            }

            creditorAmounts[ci] -= transferAmount;
            debtorAmounts[di] -= transferAmount;

            if (creditorAmounts[ci] < threshold) ci++;
            if (debtorAmounts[di] < threshold) di++;
        }

        return transfers;
    }

    #region Nested type: MemberBalanceInfo

    /// <summary>
    /// 成員餘額資訊（內部使用）
    /// </summary>
    private class MemberBalanceInfo
    {
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = string.Empty;
        public decimal TotalPaid { get; set; }
        public decimal TotalOwed { get; set; }
    }

    #endregion
}
