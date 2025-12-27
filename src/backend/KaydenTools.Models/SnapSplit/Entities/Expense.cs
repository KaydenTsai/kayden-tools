using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 費用實體
/// </summary>
public class Expense : IEntity, IAuditableEntity
{
    /// <summary>
    /// 費用 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所屬帳單 ID
    /// </summary>
    public Guid BillId { get; set; }

    /// <summary>
    /// 費用名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 服務費百分比
    /// </summary>
    public decimal ServiceFeePercent { get; set; }

    /// <summary>
    /// 是否為細項模式
    /// </summary>
    public bool IsItemized { get; set; }

    /// <summary>
    /// 付款者 ID（非細項模式使用）
    /// </summary>
    public Guid? PaidById { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>
    /// 所屬帳單
    /// </summary>
    public Bill Bill { get; set; } = null!;

    /// <summary>
    /// 付款者
    /// </summary>
    public Member? PaidBy { get; set; }

    /// <summary>
    /// 費用細項集合
    /// </summary>
    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();

    /// <summary>
    /// 分攤者集合
    /// </summary>
    public ICollection<ExpenseParticipant> Participants { get; set; } = new List<ExpenseParticipant>();
}
