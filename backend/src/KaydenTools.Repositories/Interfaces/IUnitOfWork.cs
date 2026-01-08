namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// Unit of Work 介面，負責管理資料庫交易與 Repository 存取
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// 使用者 Repository
    /// </summary>
    IUserRepository Users { get; }

    /// <summary>
    /// 刷新令牌 Repository
    /// </summary>
    IRefreshTokenRepository RefreshTokens { get; }

    /// <summary>
    /// 帳單 Repository
    /// </summary>
    IBillRepository Bills { get; }

    /// <summary>
    /// 成員 Repository
    /// </summary>
    IMemberRepository Members { get; }

    /// <summary>
    /// 費用 Repository
    /// </summary>
    IExpenseRepository Expenses { get; }

    /// <summary>
    /// 費用細項 Repository
    /// </summary>
    IExpenseItemRepository ExpenseItems { get; }

    /// <summary>
    /// 已結清轉帳 Repository
    /// </summary>
    ISettledTransferRepository SettledTransfers { get; }

    /// <summary>
    /// 操作日誌 Repository
    /// </summary>
    IOperationRepository Operations { get; }

    /// <summary>
    /// 短網址 Repository
    /// </summary>
    IShortUrlRepository ShortUrls { get; }

    /// <summary>
    /// 網址點擊記錄 Repository
    /// </summary>
    IUrlClickRepository UrlClicks { get; }

    /// <summary>
    /// 清除變更追蹤（用於並發衝突處理）
    /// </summary>
    void ClearChangeTracker();

    /// <summary>
    /// 儲存所有變更
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>受影響的資料筆數</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// 開始資料庫交易
    /// </summary>
    /// <param name="ct">取消令牌</param>
    Task BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// 提交資料庫交易
    /// </summary>
    /// <param name="ct">取消令牌</param>
    Task CommitTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// 回滾資料庫交易
    /// </summary>
    /// <param name="ct">取消令牌</param>
    Task RollbackTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// 使用執行策略在交易中執行操作（支援 NpgsqlRetryingExecutionStrategy）
    /// </summary>
    /// <typeparam name="TResult">回傳類型</typeparam>
    /// <param name="operation">要執行的操作</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作結果</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default);
}
