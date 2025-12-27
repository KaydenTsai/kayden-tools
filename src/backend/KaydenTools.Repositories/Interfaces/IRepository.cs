using System.Linq.Expressions;
using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;

namespace KaydenTools.Repositories.Interfaces;

/// <summary>
/// 通用 Repository 介面
/// </summary>
/// <typeparam name="TEntity">實體類型</typeparam>
public interface IRepository<TEntity> where TEntity : class, IEntity
{
    /// <summary>
    /// 根據 ID 取得實體
    /// </summary>
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// 根據 ID 取得實體（包含關聯資料）
    /// </summary>
    Task<TEntity?> GetByIdWithIncludesAsync(
        Guid id,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        CancellationToken ct = default);

    /// <summary>
    /// 取得所有實體
    /// </summary>
    Task<IReadOnlyList<TEntity>> GetAllAsync(
        bool asNoTracking = true,
        CancellationToken ct = default);

    /// <summary>
    /// 根據條件查詢實體
    /// </summary>
    Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        bool asNoTracking = true,
        CancellationToken ct = default);

    /// <summary>
    /// 取得符合條件的第一筆實體
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        bool asNoTracking = true,
        CancellationToken ct = default);

    /// <summary>
    /// 檢查是否存在符合條件的實體
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);

    /// <summary>
    /// 計算符合條件的實體數量
    /// </summary>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);

    /// <summary>
    /// 分頁查詢
    /// </summary>
    Task<PagedResult<TEntity>> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        bool asNoTracking = true,
        CancellationToken ct = default);

    /// <summary>
    /// 新增實體
    /// </summary>
    Task AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>
    /// 批次新增實體
    /// </summary>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

    /// <summary>
    /// 更新實體
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// 刪除實體
    /// </summary>
    void Remove(TEntity entity);

    /// <summary>
    /// 批次刪除實體
    /// </summary>
    void RemoveRange(IEnumerable<TEntity> entities);
}
