using KaydenTools.Models.UrlShortener.Entities;

namespace KaydenTools.Repositories.Interfaces;

public interface IShortUrlRepository : IRepository<ShortUrl>
{
    /// <summary>
    /// 根據短碼取得短網址
    /// </summary>
    Task<ShortUrl?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// 取得短網址及其點擊記錄
    /// </summary>
    Task<ShortUrl?> GetByIdWithClicksAsync(Guid id, int clickLimit = 100, CancellationToken ct = default);

    /// <summary>
    /// 取得使用者的所有短網址
    /// </summary>
    Task<IReadOnlyList<ShortUrl>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// 檢查短碼是否已存在
    /// </summary>
    Task<bool> ShortCodeExistsAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// 取得已過期的短網址（用於清理 Job）
    /// </summary>
    Task<IReadOnlyList<ShortUrl>> GetExpiredUrlsAsync(DateTime before, int limit, CancellationToken ct = default);

    /// <summary>
    /// 原子性增加點擊次數（避免併發遺失）
    /// </summary>
    Task IncrementClickCountAsync(Guid id, CancellationToken ct = default);
}