using System.Text.Json;
using Kayden.Commons.Interfaces;
using KaydenTools.Models.Shared.Entities;

namespace KaydenTools.Models.SnapSplit.Entities;

/// <summary>
/// 操作日誌實體 (V3 Core)
/// </summary>
public class Operation : IEntity
{
    /// <summary>
    /// 所屬帳單 ID
    /// </summary>
    public Guid BillId { get; set; }

    /// <summary>
    /// 導覽屬性：帳單
    /// </summary>
    public Bill Bill { get; set; } = null!;

    /// <summary>
    /// 版本號 (該帳單內的順序，1, 2, 3...)
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// 操作類型 (e.g., "ADD_EXPENSE", "UPDATE_MEMBER")
    /// </summary>
    public string OpType { get; set; } = string.Empty;

    /// <summary>
    /// 操作目標 ID (可選，視 OpType 而定)
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// 操作內容 (JSON)
    /// </summary>
    public JsonDocument Payload { get; set; } = default!;

    /// <summary>
    /// 建立者 User ID (若已登入)
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// 導覽屬性：建立者
    /// </summary>
    public User? CreatedByUser { get; set; }

    /// <summary>
    /// 客戶端/裝置 ID (用於解決衝突與去重)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #region IEntity Members

    /// <summary>
    /// 操作 ID (全域唯一)
    /// </summary>
    public Guid Id { get; set; }

    #endregion
}
