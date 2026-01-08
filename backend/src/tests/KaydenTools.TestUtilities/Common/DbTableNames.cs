namespace KaydenTools.TestUtilities.Common;

/// <summary>
/// 資料庫表名常量
/// 集中管理以便維護和避免硬編碼
/// </summary>
public static class DbTableNames
{
    #region SnapSplit 模組

    public const string ExpenseItemParticipants = "snapsplit.expense_item_participants";
    public const string ExpenseItems = "snapsplit.expense_items";
    public const string ExpenseParticipants = "snapsplit.expense_participants";
    public const string Expenses = "snapsplit.expenses";
    public const string SettledTransfers = "snapsplit.settled_transfers";
    public const string Members = "snapsplit.members";
    public const string Operations = "snapsplit.operations";
    public const string Bills = "snapsplit.bills";

    #endregion

    #region UrlShortener 模組

    public const string UrlClicks = "urlshortener.url_clicks";
    public const string ShortUrls = "urlshortener.short_urls";

    #endregion

    #region Shared 模組

    public const string RefreshTokens = "shared.refresh_tokens";
    public const string Users = "shared.users";

    #endregion

    /// <summary>
    /// 按外鍵依賴順序排列的所有表（子表 → 父表）
    /// 用於清空資料時確保正確順序
    /// </summary>
    public static readonly string[] AllTablesInCleanupOrder =
    {
        // SnapSplit 子表
        ExpenseItemParticipants,
        ExpenseItems,
        ExpenseParticipants,
        Expenses,
        SettledTransfers,
        Members,
        Operations,
        Bills,

        // UrlShortener 模組
        UrlClicks,
        ShortUrls,

        // Shared 模組
        RefreshTokens,
        Users,
    };
}
