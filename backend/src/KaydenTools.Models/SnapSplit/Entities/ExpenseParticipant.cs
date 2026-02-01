namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 費用分攤者（多對多關聯表）。
/// 不實作 ISoftDeletable — join table 使用 clear + rebuild 模式，soft delete 會累積無意義的孤兒記錄。
/// </summary>
public class ExpenseParticipant
{
    /// <summary>
    /// 費用 ID
    /// </summary>
    public Guid ExpenseId { get; set; }

    /// <summary>
    /// 成員 ID
    /// </summary>
    public Guid MemberId { get; set; }

    /// <summary>
    /// 分攤金額（由後端計算，使用 Penny Allocation 演算法）
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 關聯的費用
    /// </summary>
    public Expense Expense { get; set; } = null!;

    /// <summary>
    /// 關聯的成員
    /// </summary>
    public Member Member { get; set; } = null!;
}
