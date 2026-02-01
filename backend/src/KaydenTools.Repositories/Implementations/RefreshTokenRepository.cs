using Kayden.Commons.Interfaces;
using KaydenTools.Models.Shared.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    private readonly IDateTimeService _dateTimeService;

    public RefreshTokenRepository(AppDbContext context, IDateTimeService dateTimeService) : base(context)
    {
        _dateTimeService = dateTimeService;
    }

    #region IRefreshTokenRepository Members

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await DbSet
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId,
        CancellationToken ct = default)
    {
        return await DbSet
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > _dateTimeService.UtcNow)
            .ToListAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await DbSet
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        var now = _dateTimeService.UtcNow;
        foreach (var token in tokens) token.RevokedAt = now;
    }

    #endregion
}
