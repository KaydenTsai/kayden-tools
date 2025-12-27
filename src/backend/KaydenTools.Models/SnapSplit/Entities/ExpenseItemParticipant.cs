namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 費用細項分攤者（多對多關聯表）
/// </summary>
public class ExpenseItemParticipant
{
    /// <summary>
    /// 費用細項 ID
    /// </summary>
    public Guid ExpenseItemId { get; set; }

    /// <summary>
    /// 成員 ID
    /// </summary>
    public Guid MemberId { get; set; }

    /// <summary>
    /// 關聯的費用細項
    /// </summary>
    public ExpenseItem ExpenseItem { get; set; } = null!;

    /// <summary>
    /// 關聯的成員
    /// </summary>
    public Member Member { get; set; } = null!;
}
