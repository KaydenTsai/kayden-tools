using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class SettledTransferRepository : ISettledTransferRepository
{
    private readonly AppDbContext _context;
    private DbSet<SettledTransfer> DbSet => _context.SettledTransfers;

    public SettledTransferRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SettledTransfer>> GetByBillIdAsync(Guid billId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(s => s.BillId == billId)
            .Include(s => s.FromMember)
            .Include(s => s.ToMember)
            .ToListAsync(ct);
    }

    public async Task<SettledTransfer?> GetByKeyAsync(Guid billId, Guid fromMemberId, Guid toMemberId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(
            s => s.BillId == billId && s.FromMemberId == fromMemberId && s.ToMemberId == toMemberId,
            ct);
    }

    public async Task AddAsync(SettledTransfer entity)
    {
        await DbSet.AddAsync(entity);
    }

    public void Remove(SettledTransfer entity)
    {
        DbSet.Remove(entity);
    }

    public async Task RemoveByBillIdAsync(Guid billId, CancellationToken ct = default)
    {
        var entities = await DbSet.Where(s => s.BillId == billId).ToListAsync(ct);
        DbSet.RemoveRange(entities);
    }
}
