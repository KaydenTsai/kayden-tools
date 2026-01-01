namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 已結清轉帳實體（複合主鍵：bill_id, from_member_id, to_member_id）
/// </summary>
public class SettledTransfer
{
    /// <summary>
    /// 所屬帳單 ID
    /// </summary>
    public Guid BillId { get; set; }

    /// <summary>
    /// 付款方成員 ID
    /// </summary>
    public Guid FromMemberId { get; set; }

    /// <summary>
    /// 收款方成員 ID
    /// </summary>
    public Guid ToMemberId { get; set; }

    /// <summary>
    /// 結算金額
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// 結清時間
    /// </summary>
    public DateTime SettledAt { get; set; }

    /// <summary>
    /// 所屬帳單
    /// </summary>
    public Bill Bill { get; set; } = null!;

    /// <summary>
    /// 付款方成員
    /// </summary>
    public Member FromMember { get; set; } = null!;

    /// <summary>
    /// 收款方成員
    /// </summary>
    public Member ToMember { get; set; } = null!;
}
