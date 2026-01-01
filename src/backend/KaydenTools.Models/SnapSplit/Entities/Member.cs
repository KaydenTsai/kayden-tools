using Kayden.Commons.Interfaces;
using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 帳單成員實體
/// </summary>
public class Member : IEntity
{
    /// <summary>
    /// 成員 ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所屬帳單 ID
    /// </summary>
    public Guid BillId { get; set; }

    /// <summary>
    /// 成員名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 原始名稱（認領前的名稱，用於取消認領時還原）
    /// </summary>
    public string? OriginalName { get; set; }

    /// <summary>
    /// 顯示順序
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 關聯的使用者 ID（認領者）
    /// </summary>
    public Guid? LinkedUserId { get; set; }

    /// <summary>
    /// 認領時間
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 所屬帳單
    /// </summary>
    public Bill Bill { get; set; } = null!;

    /// <summary>
    /// 關聯的使用者（認領者）
    /// </summary>
    public User? LinkedUser { get; set; }
}
