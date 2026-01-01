using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 費用細項實體
/// </summary>
public class ExpenseItem : IEntity
{
    /// <summary>
    /// 細項 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所屬費用 ID
    /// </summary>
    public Guid ExpenseId { get; set; }

    /// <summary>
    /// 細項名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 付款者 ID（可為空，表示未指定付款者）
    /// </summary>
    public Guid? PaidById { get; set; }

    /// <summary>
    /// 所屬費用
    /// </summary>
    public Expense Expense { get; set; } = null!;

    /// <summary>
    /// 付款者（可為空）
    /// </summary>
    public Member? PaidBy { get; set; }

    /// <summary>
    /// 分攤者集合
    /// </summary>
    public ICollection<ExpenseItemParticipant> Participants { get; set; } = new List<ExpenseItemParticipant>();
}
