using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class MemberRepository : Repository<Member>, IMemberRepository
{
    public MemberRepository(AppDbContext context) : base(context)
    {
    }

    #region IMemberRepository Members

    public async Task<IReadOnlyList<Member>> GetByBillIdAsync(Guid billId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(m => m.BillId == billId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);
    }

    public async Task<int> GetNextDisplayOrderAsync(Guid billId, CancellationToken ct = default)
    {
        var maxOrder = await DbSet
            .Where(m => m.BillId == billId)
            .MaxAsync(m => (int?)m.DisplayOrder, ct);

        return (maxOrder ?? 0) + 1;
    }

    #endregion
}
