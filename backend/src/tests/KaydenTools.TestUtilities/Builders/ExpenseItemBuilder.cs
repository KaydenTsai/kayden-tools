using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.TestUtilities.Common;

namespace KaydenTools.TestUtilities.Builders;

/// <summary>
/// ExpenseItem 建構器
/// </summary>
public class ExpenseItemBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _expenseId = Guid.NewGuid();
    private string _name = TestFaker.RandomProductName();
    private decimal _amount = TestFaker.RandomPrice(50, 500);
    private Guid? _paidById = null;
    private readonly List<ExpenseItemParticipant> _participants = new();

    public ExpenseItemBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ExpenseItemBuilder WithExpenseId(Guid expenseId)
    {
        _expenseId = expenseId;
        return this;
    }

    public ExpenseItemBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ExpenseItemBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public ExpenseItemBuilder WithPaidById(Guid? paidById)
    {
        _paidById = paidById;
        return this;
    }

    public ExpenseItemBuilder WithPaidBy(Member member)
    {
        _paidById = member.Id;
        return this;
    }

    public ExpenseItemBuilder WithParticipant(Member member)
    {
        _participants.Add(new ExpenseItemParticipant
        {
            ExpenseItemId = _id,
            MemberId = member.Id
        });
        return this;
    }

    public ExpenseItemBuilder WithParticipants(params Member[] members)
    {
        foreach (var member in members)
        {
            _participants.Add(new ExpenseItemParticipant
            {
                ExpenseItemId = _id,
                MemberId = member.Id
            });
        }
        return this;
    }

    public ExpenseItem Build()
    {
        var item = new ExpenseItem
        {
            Id = _id,
            ExpenseId = _expenseId,
            Name = _name,
            Amount = _amount,
            PaidById = _paidById,
            Participants = _participants
        };

        // 設定導航屬性
        foreach (var participant in _participants)
        {
            participant.ExpenseItem = item;
        }

        return item;
    }
}
