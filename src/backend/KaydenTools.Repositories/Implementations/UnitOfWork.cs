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
    private ISettledTransferRepository? _settledTransfers;
    private IOperationRepository? _operations;
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
    public ISettledTransferRepository SettledTransfers => _settledTransfers ??= new SettledTransferRepository(_context);
    public IOperationRepository Operations => _operations ??= new OperationRepository(_context);

    // UrlShortener
    public IShortUrlRepository ShortUrls => _shortUrls ??= new ShortUrlRepository(_context);
    public IUrlClickRepository UrlClicks => _urlClicks ??= new UrlClickRepository(_context);

    public void ClearChangeTracker()
    {
        _context.ChangeTracker.Clear();
    }

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

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            state: (operation, ct),
            operation: async (context, state, cancellationToken) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var result = await state.operation();
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            },
            verifySucceeded: null,
            cancellationToken: ct);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
