using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
{
    #region IEntityTypeConfiguration<ExpenseItem> Members

    public void Configure(EntityTypeBuilder<ExpenseItem> builder)
    {
        builder.ToTable("expense_items", "snapsplit");

        builder.HasKey(ei => ei.Id);

        builder.Property(ei => ei.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ei => ei.Amount)
            .HasPrecision(18, 2);

        builder.Property(ei => ei.LocalClientId)
            .HasMaxLength(100);

        builder.HasOne(ei => ei.Expense)
            .WithMany(e => e.Items)
            .HasForeignKey(ei => ei.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ei => ei.PaidBy)
            .WithMany()
            .HasForeignKey(ei => ei.PaidById)
            .OnDelete(DeleteBehavior.Restrict);

        // 冪等性唯一索引：同一費用內 LocalClientId 不能重複（僅當 LocalClientId 不為 null 時）
        builder.HasIndex(ei => new { ei.ExpenseId, ei.LocalClientId })
            .HasFilter("\"LocalClientId\" IS NOT NULL")
            .IsUnique();

        // Soft Delete
        builder.Property(ei => ei.IsDeleted)
            .HasDefaultValue(false);

        builder.HasQueryFilter(ei => !ei.IsDeleted);
    }

    #endregion
}
