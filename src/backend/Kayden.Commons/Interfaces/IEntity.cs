namespace Kayden.Commons.Interfaces;

/// <summary>
/// 實體介面（自訂主鍵類型）
/// </summary>
public interface IEntity<TKey>
{
    /// <summary>
    /// 實體 ID
    /// </summary>
    TKey Id { get; set; }
}

/// <summary>
/// 實體介面（使用 Guid 作為主鍵）
/// </summary>
public interface IEntity : IEntity<Guid>
{
}
