using KaydenTools.Models.UrlShortener.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class UrlClickRepository : Repository<UrlClick>, IUrlClickRepository
{
    public UrlClickRepository(AppDbContext context) : base(context)
    {
    }

    #region IUrlClickRepository Members

    public async Task<IReadOnlyList<UrlClick>> GetByShortUrlIdAsync(
        Guid shortUrlId,
        DateTime? from = null,
        DateTime? to = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var query = DbSet.Where(c => c.ShortUrlId == shortUrlId);

        if (from.HasValue) query = query.Where(c => c.ClickedAt >= from.Value);

        if (to.HasValue) query = query.Where(c => c.ClickedAt <= to.Value);

        query = query.OrderByDescending(c => c.ClickedAt);

        if (limit.HasValue) query = query.Take(limit.Value);

        return await query.ToListAsync(ct);
    }

    public async Task<Dictionary<DateOnly, int>> GetClicksByDateAsync(
        Guid shortUrlId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        var fromDateTime = from.ToDateTime(TimeOnly.MinValue);
        var toDateTime = to.ToDateTime(TimeOnly.MaxValue);

        var results = await DbSet
            .Where(c => c.ShortUrlId == shortUrlId &&
                        c.ClickedAt >= fromDateTime &&
                        c.ClickedAt <= toDateTime)
            .GroupBy(c => c.ClickedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return results.ToDictionary(
            x => DateOnly.FromDateTime(x.Date),
            x => x.Count);
    }

    public async Task<DateTime?> GetLastClickAtAsync(Guid shortUrlId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(c => c.ShortUrlId == shortUrlId)
            .OrderByDescending(c => c.ClickedAt)
            .Select(c => (DateTime?)c.ClickedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Dictionary<string, int>> GetClicksByReferrerAsync(
        Guid shortUrlId,
        int topN = 10,
        CancellationToken ct = default)
    {
        var results = await DbSet
            .Where(c => c.ShortUrlId == shortUrlId && c.Referrer != null)
            .GroupBy(c => c.Referrer!)
            .Select(g => new { Referrer = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync(ct);

        return results.ToDictionary(x => x.Referrer, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetClicksByDeviceTypeAsync(
        Guid shortUrlId,
        CancellationToken ct = default)
    {
        var results = await DbSet
            .Where(c => c.ShortUrlId == shortUrlId)
            .GroupBy(c => c.DeviceType ?? "unknown")
            .Select(g => new { DeviceType = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return results.ToDictionary(x => x.DeviceType, x => x.Count);
    }

    #endregion
}
