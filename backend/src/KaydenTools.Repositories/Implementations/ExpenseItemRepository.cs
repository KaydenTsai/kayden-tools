using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;

namespace KaydenTools.Repositories.Implementations;

public class ExpenseItemRepository : Repository<ExpenseItem>, IExpenseItemRepository
{
    public ExpenseItemRepository(AppDbContext context) : base(context)
    {
    }
}
