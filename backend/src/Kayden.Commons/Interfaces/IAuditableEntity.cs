namespace Kayden.Commons.Interfaces;

/// <summary>
/// 可審計實體介面
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// 建立時間
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新時間
    /// </summary>
    DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 建立者 ID
    /// </summary>
    Guid? CreatedBy { get; set; }

    /// <summary>
    /// 更新者 ID
    /// </summary>
    Guid? UpdatedBy { get; set; }
}
