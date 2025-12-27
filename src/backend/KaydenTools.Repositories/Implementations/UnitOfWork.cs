using KaydenTools.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace KaydenTools.Repositories.Implementations;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private IUserRepository? _users;
    private IRefreshTokenRepository? _refreshTokens;
    private IBillRepository? _bills;
    private IMemberRepository? _members;
    private IExpenseRepository? _expenses;
    private ISettlementRepository? _settlements;
    private IShortUrlRepository? _shortUrls;
    private IUrlClickRepository? _urlClicks;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(_context);
    public IBillRepository Bills => _bills ??= new BillRepository(_context);
    public IMemberRepository Members => _members ??= new MemberRepository(_context);
    public IExpenseRepository Expenses => _expenses ??= new ExpenseRepository(_context);
    public ISettlementRepository Settlements => _settlements ??= new SettlementRepository(_context);

    // UrlShortener
    public IShortUrlRepository ShortUrls => _shortUrls ??= new ShortUrlRepository(_context);
    public IUrlClickRepository UrlClicks => _urlClicks ??= new UrlClickRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
