using Kayden.Commons.Interfaces;
using KaydenTools.Core.Interfaces;
using KaydenTools.Models.Shared.Entities;
using KaydenTools.Models.SnapSplit.Entities;
using KaydenTools.Models.UrlShortener.Entities;
using Microsoft.EntityFrameworkCore;

namespace KaydenTools.Repositories;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IDateTimeService dateTimeService,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _dateTimeService = dateTimeService;
        _currentUserService = currentUserService;
    }

    // Shared
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // SnapSplit
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<ExpenseParticipant> ExpenseParticipants => Set<ExpenseParticipant>();
    public DbSet<ExpenseItemParticipant> ExpenseItemParticipants => Set<ExpenseItemParticipant>();
    public DbSet<SettledTransfer> SettledTransfers => Set<SettledTransfer>();
    public DbSet<Operation> Operations => Set<Operation>();

    // UrlShortener
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    public DbSet<UrlClick> UrlClicks => Set<UrlClick>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations from current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditableEntity &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (IAuditableEntity)entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = _dateTimeService.UtcNow;
                entity.CreatedBy = _currentUserService.UserId;
            }

            entity.UpdatedAt = _dateTimeService.UtcNow;
            entity.UpdatedBy = _currentUserService.UserId;
        }

        // Handle soft delete
        var deletedEntries = ChangeTracker.Entries()
            .Where(e => e.Entity is ISoftDeletable && e.State == EntityState.Deleted);

        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;
            var entity = (ISoftDeletable)entry.Entity;
            entity.IsDeleted = true;
            entity.DeletedAt = _dateTimeService.UtcNow;
            entity.DeletedBy = _currentUserService.UserId;
        }
    }
}
