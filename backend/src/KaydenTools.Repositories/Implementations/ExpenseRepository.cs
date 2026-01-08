using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class ExpenseRepository : Repository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext context) : base(context)
    {
    }

    #region IExpenseRepository Members

    public async Task<Expense?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(e => e.Participants)
            .Include(e => e.Items)
            .ThenInclude(i => i.Participants)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<Expense>> GetByBillIdAsync(Guid billId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(e => e.BillId == billId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Expense>> GetByBillIdWithDetailsAsync(Guid billId, CancellationToken ct = default)
    {
        return await DbSet
            .Where(e => e.BillId == billId)
            .Include(e => e.Participants)
            .Include(e => e.Items)
            .ThenInclude(i => i.Participants)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    #endregion
}
