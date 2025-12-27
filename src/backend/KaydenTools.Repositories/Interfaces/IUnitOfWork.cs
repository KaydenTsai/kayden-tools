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
    /// 結算 Repository
    /// </summary>
    ISettlementRepository Settlements { get; }

    /// <summary>
    /// 短網址 Repository
    /// </summary>
    IShortUrlRepository ShortUrls { get; }

    /// <summary>
    /// 網址點擊記錄 Repository
    /// </summary>
    IUrlClickRepository UrlClicks { get; }

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
}
