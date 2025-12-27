using KaydenTools.Models.UrlShortener.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class ShortUrlRepository : Repository<ShortUrl>, IShortUrlRepository
{
    public ShortUrlRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ShortUrl?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(s => s.ShortCode == shortCode, ct);
    }

    public async Task<ShortUrl?> GetByIdWithClicksAsync(Guid id, int clickLimit = 100, CancellationToken ct = default)
    {
        return await DbSet
            .Include(s => s.Clicks
                .OrderByDescending(c => c.ClickedAt)
                .Take(clickLimit))
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<ShortUrl>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(s => s.OwnerId == ownerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> ShortCodeExistsAsync(string shortCode, CancellationToken ct = default)
    {
        return await DbSet.AnyAsync(s => s.ShortCode == shortCode, ct);
    }

    public async Task<IReadOnlyList<ShortUrl>> GetExpiredUrlsAsync(DateTime before, int limit, CancellationToken ct = default)
    {
        return await DbSet
            .IgnoreQueryFilters() // 包含軟刪除的項目以便清理
            .Where(s => s.ExpiresAt != null && s.ExpiresAt < before && !s.IsDeleted)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <summary>
    /// 使用 SQL 原子性更新點擊次數，避免併發時遺失更新
    /// </summary>
    public async Task IncrementClickCountAsync(Guid id, CancellationToken ct = default)
    {
        await Context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE urlshortener.short_urls SET click_count = click_count + 1 WHERE id = {id}",
            ct);
    }
}