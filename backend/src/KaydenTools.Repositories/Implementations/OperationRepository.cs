using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Repositories.Interfaces;

namespace KaydenTools.Repositories.Implementations;

public class OperationRepository : Repository<Operation>, IOperationRepository
{
    public OperationRepository(AppDbContext context) : base(context)
    {
    }
}
