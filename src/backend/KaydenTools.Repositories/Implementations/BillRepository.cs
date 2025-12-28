using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class BillRepository : Repository<Bill>, IBillRepository
{
    public BillRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Bill?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
                .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
                .ThenInclude(e => e.Items)
                    .ThenInclude(i => i.Participants)
            .Include(b => b.Settlements)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Bill?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default)
    {
        return await DbSet
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
                .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
                .ThenInclude(e => e.Items)
                    .ThenInclude(i => i.Participants)
            .Include(b => b.Settlements)
            .FirstOrDefaultAsync(b => b.ShareCode == shareCode, ct);
    }

    public async Task<IReadOnlyList<Bill>> GetByOwnerIdAsync(Guid ownerId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Bill>> GetByLinkedUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
                .ThenInclude(m => m.LinkedUser)
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
                .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
                .ThenInclude(e => e.Items)
                    .ThenInclude(i => i.Participants)
            .Include(b => b.Settlements)
            .Where(b => b.Members.Any(m => m.LinkedUserId == userId) || b.OwnerId == userId)
            .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            .ToListAsync(ct);
    }
}
