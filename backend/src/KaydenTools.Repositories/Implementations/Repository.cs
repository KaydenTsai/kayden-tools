using System.Linq.Expressions;
using Kayden.Commons.Common;
using Kayden.Commons.Interfaces;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class, IEntity
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public Repository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    #region IRepository<TEntity> Members

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<TEntity?> GetByIdWithIncludesAsync(
        Guid id,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet;

        if (include != null) query = include(query);

        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(
        bool asNoTracking = true,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet;

        if (asNoTracking) query = query.AsNoTracking();

        return await query.ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        bool asNoTracking = true,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet;

        if (asNoTracking) query = query.AsNoTracking();

        if (include != null) query = include(query);

        query = query.Where(predicate);

        if (orderBy != null) query = orderBy(query);

        return await query.ToListAsync(ct);
    }

    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        bool asNoTracking = true,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet;

        if (asNoTracking) query = query.AsNoTracking();

        if (include != null) query = include(query);

        return await query.FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(predicate, ct);
    }

    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        return predicate == null
            ? await DbSet.CountAsync(ct)
            : await DbSet.CountAsync(predicate, ct);
    }

    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include = null,
        bool asNoTracking = true,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = DbSet;

        if (asNoTracking) query = query.AsNoTracking();

        if (include != null) query = include(query);

        if (predicate != null) query = query.Where(predicate);

        var totalCount = await query.CountAsync(ct);

        if (orderBy != null) query = orderBy(query);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<TEntity>(items, totalCount, page, pageSize);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        await DbSet.AddRangeAsync(entities, ct);
    }

    public virtual void Update(TEntity entity)
    {
        DbSet.Update(entity);
    }

    public virtual void Remove(TEntity entity)
    {
        DbSet.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<TEntity> entities)
    {
        DbSet.RemoveRange(entities);
    }

    #endregion
}
