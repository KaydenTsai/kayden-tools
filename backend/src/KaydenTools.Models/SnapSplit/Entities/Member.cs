using Kayden.Commons.Interfaces;
using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 帳單成員實體
/// </summary>
public class Member : IEntity, ISoftDeletable
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

    #region IEntity Members

    /// <summary>
    /// 成員 ID
    /// </summary>
    public Guid Id { get; set; }

    #endregion

    #region ISoftDeletable Members

    /// <summary>
    /// 是否已刪除
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 刪除時間
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 刪除者 ID
    /// </summary>
    public Guid? DeletedBy { get; set; }

    #endregion
}
