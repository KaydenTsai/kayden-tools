using Kayden.Commons.Common;
using KaydenTools.Models.UrlShortener.Dtos;

namespace KaydenTools.Services.Interfaces;

public interface IShortUrlService
{
    /// <summary>
    /// 建立短網址
    /// </summary>
    Task<Result<ShortUrlDto>> CreateAsync(CreateShortUrlDto dto, Guid? ownerId, CancellationToken ct = default);

    /// <summary>
    /// 根據 ID 取得短網址
    /// </summary>
    Task<Result<ShortUrlDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 根據短碼取得短網址
    /// </summary>
    Task<Result<ShortUrlDto>> GetByShortCodeAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// 取得使用者的所有短網址
    /// </summary>
    Task<Result<IReadOnlyList<ShortUrlSummaryDto>>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// 更新短網址
    /// </summary>
    Task<Result<ShortUrlDto>> UpdateAsync(Guid id, UpdateShortUrlDto dto, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// 刪除短網址
    /// </summary>
    Task<Result> DeleteAsync(Guid id, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// 解析短碼並回傳原始 URL（不追蹤點擊）
    /// </summary>
    Task<Result<string>> ResolveAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// 取得短網址統計
    /// </summary>
    Task<Result<UrlStatsDto>> GetStatsAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct = default);
}