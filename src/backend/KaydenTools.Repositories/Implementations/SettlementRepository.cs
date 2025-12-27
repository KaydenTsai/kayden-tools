using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class SettlementRepository : Repository<Settlement>, ISettlementRepository
{
    public SettlementRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Settlement>> GetByBillIdAsync(Guid billId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(s => s.BillId == billId)
            .Include(s => s.FromMember)
            .Include(s => s.ToMember)
            .ToListAsync(ct);
    }

    public async Task<Settlement?> GetByMembersAsync(Guid billId, Guid fromMemberId, Guid toMemberId, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(
            s => s.BillId == billId && s.FromMemberId == fromMemberId && s.ToMemberId == toMemberId,
            ct);
    }
}
