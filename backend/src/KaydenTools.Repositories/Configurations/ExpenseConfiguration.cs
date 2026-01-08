using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    #region IEntityTypeConfiguration<Expense> Members

    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses", "snapsplit");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Property(e => e.ServiceFeePercent)
            .HasPrecision(5, 2);

        builder.Property(e => e.LocalClientId)
            .HasMaxLength(100);

        builder.HasOne(e => e.Bill)
            .WithMany(b => b.Expenses)
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.PaidBy)
            .WithMany()
            .HasForeignKey(e => e.PaidById)
            .OnDelete(DeleteBehavior.SetNull);

        // 冪等性唯一索引：同一帳單內 LocalClientId 不能重複（僅當 LocalClientId 不為 null 時）
        builder.HasIndex(e => new { e.BillId, e.LocalClientId })
            .HasFilter("\"LocalClientId\" IS NOT NULL")
            .IsUnique();

        // Soft Delete
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }

    #endregion
}
