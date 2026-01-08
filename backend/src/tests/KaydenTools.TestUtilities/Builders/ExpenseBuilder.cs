using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.TestUtilities.Common;

namespace KaydenTools.TestUtilities.Builders;

/// <summary>
/// Expense 建構器
/// </summary>
public class ExpenseBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _billId = Guid.NewGuid();
    private string _name = TestFaker.RandomProductName();
    private decimal _amount = TestFaker.RandomPrice(100, 1000);
    private decimal _serviceFeePercent = 0;
    private bool _isItemized = false;
    private Guid? _paidById = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt = DateTime.UtcNow;
    private readonly List<ExpenseParticipant> _participants = new();
    private readonly List<ExpenseItem> _items = new();

    public ExpenseBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ExpenseBuilder WithBillId(Guid billId)
    {
        _billId = billId;
        return this;
    }

    public ExpenseBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ExpenseBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public ExpenseBuilder WithServiceFeePercent(decimal percent)
    {
        _serviceFeePercent = percent;
        return this;
    }

    public ExpenseBuilder WithIsItemized(bool isItemized)
    {
        _isItemized = isItemized;
        return this;
    }

    public ExpenseBuilder WithPaidById(Guid? paidById)
    {
        _paidById = paidById;
        return this;
    }

    public ExpenseBuilder WithPaidBy(Member member)
    {
        _paidById = member.Id;
        return this;
    }

    public ExpenseBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public ExpenseBuilder WithParticipant(Member member)
    {
        _participants.Add(new ExpenseParticipant
        {
            ExpenseId = _id,
            MemberId = member.Id
        });
        return this;
    }

    public ExpenseBuilder WithParticipants(params Member[] members)
    {
        foreach (var member in members)
        {
            _participants.Add(new ExpenseParticipant
            {
                ExpenseId = _id,
                MemberId = member.Id
            });
        }
        return this;
    }

    public ExpenseBuilder WithItem(ExpenseItem item)
    {
        item.ExpenseId = _id;
        _items.Add(item);
        _isItemized = true;
        return this;
    }

    public ExpenseBuilder WithItems(params ExpenseItem[] items)
    {
        foreach (var item in items)
        {
            item.ExpenseId = _id;
            _items.Add(item);
        }
        _isItemized = true;
        return this;
    }

    public Expense Build()
    {
        var expense = new Expense
        {
            Id = _id,
            BillId = _billId,
            Name = _name,
            Amount = _amount,
            ServiceFeePercent = _serviceFeePercent,
            IsItemized = _isItemized,
            PaidById = _paidById,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            Participants = _participants,
            Items = _items
        };

        // 設定導航屬性
        foreach (var participant in _participants)
        {
            participant.Expense = expense;
        }
        foreach (var item in _items)
        {
            item.Expense = expense;
        }

        return expense;
    }
}
