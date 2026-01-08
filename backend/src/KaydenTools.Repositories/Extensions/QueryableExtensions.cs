using Kayden.Commons.Common;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Extensions;

/// <summary>
/// IQueryable 擴充方法
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// 將查詢結果轉換為分頁結果
    /// </summary>
    /// <typeparam name="T">項目類型</typeparam>
    /// <param name="query">查詢</param>
    /// <param name="page">頁碼（從 1 開始）</param>
    /// <param name="pageSize">每頁筆數（小於等於 0 表示不分頁）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分頁結果</returns>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);

        // 不分頁場景
        if (pageSize <= 0)
        {
            var allItems = await query.ToListAsync(ct);
            return PagedResult<T>.All(allItems);
        }

        // 分頁查詢
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<T>(items, totalCount, page, pageSize);
    }
}
