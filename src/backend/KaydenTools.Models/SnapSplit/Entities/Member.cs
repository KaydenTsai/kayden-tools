using Kayden.Commons.Interfaces;

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
    /// 顯示順序
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// 關聯的使用者 ID
    /// </summary>
    public Guid? LinkedUserId { get; set; }

    /// <summary>
    /// 所屬帳單
    /// </summary>
    public Bill Bill { get; set; } = null!;
}
