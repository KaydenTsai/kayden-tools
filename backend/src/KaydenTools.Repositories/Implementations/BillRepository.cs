using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class BillRepository : Repository<Bill>, IBillRepository
{
    public BillRepository(AppDbContext context) : base(context)
    {
    }

    #region IBillRepository Members

    public async Task<Bill?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
            .ThenInclude(m => m.LinkedUser)
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
            .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
            .ThenInclude(e => e.Items)
            .ThenInclude(i => i.Participants)
            .Include(b => b.SettledTransfers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Bill?> GetByShareCodeAsync(string shareCode, CancellationToken ct = default)
    {
        return await DbSet
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
            .ThenInclude(m => m.LinkedUser)
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
            .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
            .ThenInclude(e => e.Items)
            .ThenInclude(i => i.Participants)
            .Include(b => b.SettledTransfers)
            .AsSplitQuery()
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
            .Include(b => b.SettledTransfers)
            .AsSplitQuery()
            .Where(b => b.Members.Any(m => m.LinkedUserId == userId) || b.OwnerId == userId)
            .OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<long?> GetCurrentVersionAsync(Guid id, CancellationToken ct = default)
    {
        // 使用 AsNoTracking 確保直接從資料庫查詢，不使用快取
        return await DbSet
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => (long?)b.Version)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Bill?> GetByIdWithLockAsync(Guid id, CancellationToken ct = default)
    {
        // 使用 raw SQL 執行 SELECT FOR UPDATE 來鎖定該列
        // 這會在當前事務期間阻止其他事務修改此列
        // 注意：必須在事務中使用才有效
        var bill = await Context.Set<Bill>()
            .FromSqlInterpolated($@"
                SELECT * FROM snapsplit.bills
                WHERE id = {id} AND is_deleted = false
                FOR UPDATE")
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
            .ThenInclude(m => m.LinkedUser)
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
            .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
            .ThenInclude(e => e.Items)
            .ThenInclude(i => i.Participants)
            .Include(b => b.SettledTransfers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);

        return bill;
    }

    public async Task<Bill?> GetByLocalClientIdAndOwnerAsync(string localClientId, Guid ownerId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(b => b.Members.OrderBy(m => m.DisplayOrder))
            .ThenInclude(m => m.LinkedUser)
            .Include(b => b.Expenses.OrderByDescending(e => e.CreatedAt))
            .ThenInclude(e => e.Participants)
            .Include(b => b.Expenses)
            .ThenInclude(e => e.Items)
            .ThenInclude(i => i.Participants)
            .Include(b => b.SettledTransfers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.LocalClientId == localClientId && b.OwnerId == ownerId, ct);
    }

    #endregion
}
