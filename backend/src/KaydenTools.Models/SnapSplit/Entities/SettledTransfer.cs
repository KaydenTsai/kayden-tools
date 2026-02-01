namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 已結清轉帳實體（複合主鍵：bill_id, from_member_id, to_member_id）。
/// 不實作 ISoftDeletable — unmark 語意為「轉帳尚未完成」，刪除記錄語意正確；操作歷史由 Operation 追蹤。
/// </summary>
public class SettledTransfer
{
    /// <summary>
    /// Key 格式分隔符（與前端保持一致）
    /// </summary>
    public const string KeySeparator = "::";

    /// <summary>
    /// 將 SettledTransfer 格式化為字串 key（含金額）
    /// </summary>
    public string ToKeyString() => $"{FromMemberId}{KeySeparator}{ToMemberId}:{Amount:F2}";

    /// <summary>
    /// 靜態方法：格式化為字串 key
    /// </summary>
    public static string FormatKey(Guid fromId, Guid toId, decimal amount) =>
        $"{fromId}{KeySeparator}{toId}:{amount:F2}";

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
