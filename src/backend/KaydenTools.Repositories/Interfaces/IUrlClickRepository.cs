using KaydenTools.Models.UrlShortener.Entities;

namespace KaydenTools.Repositories.Interfaces;

public interface IUrlClickRepository : IRepository<UrlClick>
{
    /// <summary>
    /// 取得指定短網址的點擊記錄
    /// </summary>
    Task<IReadOnlyList<UrlClick>> GetByShortUrlIdAsync(
        Guid shortUrlId,
        DateTime? from = null,
        DateTime? to = null,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// 取得按日期分組的點擊統計
    /// </summary>
    Task<Dictionary<DateOnly, int>> GetClicksByDateAsync(
        Guid shortUrlId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    /// <summary>
    /// 取得最後一次點擊時間
    /// </summary>
    Task<DateTime?> GetLastClickAtAsync(Guid shortUrlId, CancellationToken ct = default);

    /// <summary>
    /// 取得按 Referrer 分組的點擊統計
    /// </summary>
    Task<Dictionary<string, int>> GetClicksByReferrerAsync(
        Guid shortUrlId,
        int topN = 10,
        CancellationToken ct = default);

    /// <summary>
    /// 取得按裝置類型分組的點擊統計
    /// </summary>
    Task<Dictionary<string, int>> GetClicksByDeviceTypeAsync(
        Guid shortUrlId,
        CancellationToken ct = default);
}