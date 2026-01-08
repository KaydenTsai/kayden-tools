using KaydenTools.Models.Shared.Entities;
using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories.Implementations;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    #region IUserRepository Members

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await DbSet.FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    public async Task<User?> GetByLineUserIdAsync(string lineUserId, CancellationToken ct = default)
    {
        // 忽略軟刪除過濾器，避免找不到已刪除的使用者而造成重複新增
        return await DbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.LineUserId == lineUserId, ct);
    }

    public async Task<User?> GetByGoogleUserIdAsync(string googleUserId, CancellationToken ct = default)
    {
        // 忽略軟刪除過濾器，避免找不到已刪除的使用者而造成重複新增
        return await DbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.GoogleUserId == googleUserId, ct);
    }

    #endregion
}
