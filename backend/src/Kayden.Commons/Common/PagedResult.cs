using System.Text.Json.Serialization;

namespace Kayden.Commons.Common;

/// <summary>
/// 分頁結果
/// </summary>
public class PagedResult<T>
{
    /// <summary>
    /// 建構函式
    /// </summary>
    /// <param name="items">項目清單</param>
    /// <param name="totalCount">總筆數</param>
    /// <param name="page">目前頁碼</param>
    /// <param name="pageSize">每頁筆數</param>
    [JsonConstructor]
    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// 項目清單
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// 總筆數
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// 目前頁碼
    /// </summary>
    public int Page { get; }

    /// <summary>
    /// 每頁筆數
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// 總頁數
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    /// <summary>
    ///     是否有上一頁
    /// </summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    ///     是否有下一頁
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    ///     建立空的分頁結果
    /// </summary>
    /// <param name="page">頁碼</param>
    /// <param name="pageSize">每頁筆數</param>
    /// <returns>空的分頁結果</returns>
    public static PagedResult<T> Empty(int page, int pageSize)
    {
        return new PagedResult<T>([], 0, page, pageSize);
    }

    /// <summary>
    ///     建立包含所有項目的結果（不分頁場景）
    /// </summary>
    /// <param name="items">所有項目</param>
    /// <returns>包含所有項目的分頁結果</returns>
    public static PagedResult<T> All(IReadOnlyList<T> items)
    {
        return new PagedResult<T>(items, items.Count, 1, items.Count > 0 ? items.Count : 1);
    }

    /// <summary>
    ///     將項目轉換為另一種類型
    /// </summary>
    /// <typeparam name="TResult">目標類型</typeparam>
    /// <param name="selector">轉換函式</param>
    /// <returns>轉換後的分頁結果</returns>
    public PagedResult<TResult> Map<TResult>(Func<T, TResult> selector)
    {
        var mappedItems = Items.Select(selector).ToList();
        return new PagedResult<TResult>(mappedItems, TotalCount, Page, PageSize);
    }
}
