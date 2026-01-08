using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.TestUtilities.Common;

namespace KaydenTools.TestUtilities.Builders;

/// <summary>
/// Member 建構器
/// </summary>
public class MemberBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _billId = Guid.NewGuid();
    private string _name = TestFaker.RandomName();
    private string? _originalName = null;
    private int _displayOrder = 0;
    private Guid? _linkedUserId = null;
    private DateTime? _claimedAt = null;
    private DateTime? _updatedAt = DateTime.UtcNow;

    public MemberBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public MemberBuilder WithBillId(Guid billId)
    {
        _billId = billId;
        return this;
    }

    public MemberBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public MemberBuilder WithOriginalName(string? originalName)
    {
        _originalName = originalName;
        return this;
    }

    public MemberBuilder WithDisplayOrder(int displayOrder)
    {
        _displayOrder = displayOrder;
        return this;
    }

    public MemberBuilder WithLinkedUserId(Guid? linkedUserId)
    {
        _linkedUserId = linkedUserId;
        if (linkedUserId.HasValue)
        {
            _claimedAt ??= DateTime.UtcNow;
        }
        return this;
    }

    public MemberBuilder WithClaimedAt(DateTime? claimedAt)
    {
        _claimedAt = claimedAt;
        return this;
    }

    public MemberBuilder AsClaimed(Guid userId)
    {
        _linkedUserId = userId;
        _claimedAt = DateTime.UtcNow;
        _originalName = _name;
        return this;
    }

    public Member Build()
    {
        return new Member
        {
            Id = _id,
            BillId = _billId,
            Name = _name,
            OriginalName = _originalName,
            DisplayOrder = _displayOrder,
            LinkedUserId = _linkedUserId,
            ClaimedAt = _claimedAt,
            UpdatedAt = _updatedAt
        };
    }
}
