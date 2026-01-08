using Bogus;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.TestUtilities.Common;

namespace KaydenTools.TestUtilities.Base;

/// <summary>
/// 單元測試基底類別
/// 不需要資料庫，執行快速
/// </summary>
public abstract class UnitTestBase
{
    /// <summary>
    /// 共用的假資料產生器
    /// </summary>
    protected static Faker Faker => TestFaker.Instance;

    #region Make Methods - 建立內存物件（不存入 DB）

    /// <summary>
    /// 建立帳單（內存物件）
    /// </summary>
    protected Bill MakeBill(
        string? name = null,
        Guid? ownerId = null,
        Action<Bill>? customize = null)
    {
        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            Name = name ?? Faker.Commerce.ProductName(),
            OwnerId = ownerId,
            ShareCode = GenerateShareCode(),
            Version = 1,
            IsSettled = false,
            CreatedAt = DateTime.UtcNow,
            Members = new List<Member>(),
            Expenses = new List<Expense>(),
            SettledTransfers = new List<SettledTransfer>()
        };

        customize?.Invoke(bill);
        return bill;
    }

    /// <summary>
    /// 建立成員（內存物件）
    /// </summary>
    protected Member MakeMember(
        string? name = null,
        int displayOrder = 0,
        Guid? linkedUserId = null,
        Action<Member>? customize = null)
    {
        var member = new Member
        {
            Id = Guid.NewGuid(),
            Name = name ?? Faker.Name.FirstName(),
            DisplayOrder = displayOrder,
            LinkedUserId = linkedUserId,
            ClaimedAt = linkedUserId.HasValue ? DateTime.UtcNow : null,
            UpdatedAt = DateTime.UtcNow
        };

        customize?.Invoke(member);
        return member;
    }

    /// <summary>
    /// 建立費用（內存物件）
    /// </summary>
    protected Expense MakeExpense(
        string? name = null,
        decimal? amount = null,
        Guid? paidById = null,
        bool isItemized = false,
        Action<Expense>? customize = null)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            Name = name ?? Faker.Commerce.ProductName(),
            Amount = amount ?? RandomPrice(100, 1000),
            ServiceFeePercent = 0,
            IsItemized = isItemized,
            PaidById = paidById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Participants = new List<ExpenseParticipant>(),
            Items = new List<ExpenseItem>()
        };

        customize?.Invoke(expense);
        return expense;
    }

    /// <summary>
    /// 建立費用細項（內存物件）
    /// </summary>
    protected ExpenseItem MakeExpenseItem(
        string? name = null,
        decimal? amount = null,
        Guid? paidById = null,
        Action<ExpenseItem>? customize = null)
    {
        var item = new ExpenseItem
        {
            Id = Guid.NewGuid(),
            Name = name ?? Faker.Commerce.ProductName(),
            Amount = amount ?? RandomPrice(50, 500),
            PaidById = paidById,
            Participants = new List<ExpenseItemParticipant>()
        };

        customize?.Invoke(item);
        return item;
    }

    /// <summary>
    /// 建立費用參與者（內存物件）
    /// </summary>
    protected ExpenseParticipant MakeExpenseParticipant(
        Expense expense,
        Member member)
    {
        return new ExpenseParticipant
        {
            Expense = expense,
            ExpenseId = expense.Id,
            MemberId = member.Id
        };
    }

    /// <summary>
    /// 建立費用細項參與者（內存物件）
    /// </summary>
    protected ExpenseItemParticipant MakeExpenseItemParticipant(
        ExpenseItem item,
        Member member)
    {
        return new ExpenseItemParticipant
        {
            ExpenseItem = item,
            ExpenseItemId = item.Id,
            MemberId = member.Id
        };
    }

    /// <summary>
    /// 建立已結清轉帳（內存物件）
    /// </summary>
    protected SettledTransfer MakeSettledTransfer(
        Guid billId,
        Guid fromMemberId,
        Guid toMemberId,
        decimal amount = 0)
    {
        return new SettledTransfer
        {
            BillId = billId,
            FromMemberId = fromMemberId,
            ToMemberId = toMemberId,
            Amount = amount,
            SettledAt = DateTime.UtcNow
        };
    }

    #endregion

    #region Random Data Helpers

    /// <summary>
    /// 產生隨機價格
    /// </summary>
    protected decimal RandomPrice(decimal min = 10m, decimal max = 10000m)
        => Math.Round(Faker.Random.Decimal(min, max), 2);

    /// <summary>
    /// 產生隨機百分比（0-100）
    /// </summary>
    protected decimal RandomPercent(decimal min = 0m, decimal max = 20m)
        => Math.Round(Faker.Random.Decimal(min, max), 1);

    /// <summary>
    /// 產生隨機分享碼（8 字元）
    /// </summary>
    protected string GenerateShareCode()
        => Faker.Random.AlphaNumeric(8).ToUpper();

    /// <summary>
    /// 產生隨機名稱
    /// </summary>
    protected string RandomName()
        => Faker.Name.FirstName();

    /// <summary>
    /// 產生隨機商品名稱
    /// </summary>
    protected string RandomProductName()
        => Faker.Commerce.ProductName();

    #endregion

    #region Helper Methods

    /// <summary>
    /// 建立完整的帳單（含成員和費用）
    /// </summary>
    protected Bill MakeCompleteBill(
        int memberCount = 2,
        int expenseCount = 1,
        Action<Bill>? customize = null)
    {
        var bill = MakeBill();

        // 建立成員
        for (int i = 0; i < memberCount; i++)
        {
            var member = MakeMember(displayOrder: i);
            member.BillId = bill.Id;
            bill.Members.Add(member);
        }

        // 建立費用
        var memberList = bill.Members.ToList();
        for (int i = 0; i < expenseCount; i++)
        {
            var payer = memberList[i % memberCount];
            var expense = MakeExpense(paidById: payer.Id);
            expense.BillId = bill.Id;

            // 所有成員都是參與者
            foreach (var member in bill.Members)
            {
                expense.Participants.Add(MakeExpenseParticipant(expense, member));
            }

            bill.Expenses.Add(expense);
        }

        customize?.Invoke(bill);
        return bill;
    }

    #endregion
}
