namespace Kayden.Commons.Interfaces;

/// <summary>
/// 軟刪除介面
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// 是否已刪除
    /// </summary>
    bool IsDeleted { get; set; }

    /// <summary>
    /// 刪除時間
    /// </summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>
    /// 刪除者 ID
    /// </summary>
    Guid? DeletedBy { get; set; }
}
