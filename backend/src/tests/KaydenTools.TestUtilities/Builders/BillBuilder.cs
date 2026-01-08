using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.TestUtilities.Common;

namespace KaydenTools.TestUtilities.Builders;

/// <summary>
/// Bill 建構器
/// </summary>
public class BillBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = TestFaker.RandomProductName();
    private string _shareCode = TestFaker.GenerateShareCode();
    private long _version = 1;
    private bool _isSettled = false;
    private Guid? _ownerId = null;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _updatedAt = null;
    private readonly List<Member> _members = new();
    private readonly List<Expense> _expenses = new();
    private readonly List<SettledTransfer> _settledTransfers = new();

    public BillBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public BillBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public BillBuilder WithShareCode(string shareCode)
    {
        _shareCode = shareCode;
        return this;
    }

    public BillBuilder WithVersion(long version)
    {
        _version = version;
        return this;
    }

    public BillBuilder WithIsSettled(bool isSettled)
    {
        _isSettled = isSettled;
        return this;
    }

    public BillBuilder WithOwnerId(Guid? ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public BillBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public BillBuilder WithUpdatedAt(DateTime? updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public BillBuilder WithMember(Member member)
    {
        member.BillId = _id;
        _members.Add(member);
        return this;
    }

    public BillBuilder WithMembers(params Member[] members)
    {
        foreach (var member in members)
        {
            member.BillId = _id;
            _members.Add(member);
        }
        return this;
    }

    public BillBuilder WithExpense(Expense expense)
    {
        expense.BillId = _id;
        _expenses.Add(expense);
        return this;
    }

    public BillBuilder WithExpenses(params Expense[] expenses)
    {
        foreach (var expense in expenses)
        {
            expense.BillId = _id;
            _expenses.Add(expense);
        }
        return this;
    }

    public Bill Build()
    {
        return new Bill
        {
            Id = _id,
            Name = _name,
            ShareCode = _shareCode,
            Version = _version,
            IsSettled = _isSettled,
            OwnerId = _ownerId,
            CreatedAt = _createdAt,
            UpdatedAt = _updatedAt,
            Members = _members,
            Expenses = _expenses,
            SettledTransfers = _settledTransfers
        };
    }
}
