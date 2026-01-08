using Kayden.Commons.Interfaces;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 費用實體
/// </summary>
public class Expense : IEntity, IAuditableEntity, ISoftDeletable
{
    /// <summary>
    /// 所屬帳單 ID
    /// </summary>
    public Guid BillId { get; set; }

    /// <summary>
    /// 客戶端本地 ID（用於冪等性檢查）
    /// </summary>
    public string? LocalClientId { get; set; }

    /// <summary>
    /// 費用名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 金額
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    ///     服務費百分比
    /// </summary>
    public decimal ServiceFeePercent { get; set; }

    /// <summary>
    ///     是否為細項模式
    /// </summary>
    public bool IsItemized { get; set; }

    /// <summary>
    ///     付款者 ID（非細項模式使用）
    /// </summary>
    public Guid? PaidById { get; set; }

    /// <summary>
    ///     所屬帳單
    /// </summary>
    public Bill Bill { get; set; } = null!;

    /// <summary>
    ///     付款者
    /// </summary>
    public Member? PaidBy { get; set; }

    /// <summary>
    ///     費用細項集合
    /// </summary>
    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();

    /// <summary>
    ///     分攤者集合
    /// </summary>
    public ICollection<ExpenseParticipant> Participants { get; set; } = new List<ExpenseParticipant>();

    #region IAuditableEntity Members

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    #endregion

    #region IEntity Members

    /// <summary>
    ///     費用 ID
    /// </summary>
    public Guid Id { get; set; }

    #endregion

    #region ISoftDeletable Members

    /// <summary>
    ///     是否已刪除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    ///     刪除時間
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    ///     刪除者 ID
    /// </summary>
    public Guid? DeletedBy { get; set; }

    #endregion
}
