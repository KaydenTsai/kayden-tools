using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 帳單實體
/// </summary>
public class Bill : IEntity, IAuditableEntity, ISoftDeletable
{
    /// <summary>
    /// 帳單 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 帳單名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 擁有者 ID
    /// </summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// 分享碼
    /// </summary>
    public string? ShareCode { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>
    /// 成員集合
    /// </summary>
    public ICollection<Member> Members { get; set; } = new List<Member>();

    /// <summary>
    /// 費用集合
    /// </summary>
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();

    /// <summary>
    /// 結算集合
    /// </summary>
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}
