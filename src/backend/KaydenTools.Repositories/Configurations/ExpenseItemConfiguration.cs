using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
{
    public void Configure(EntityTypeBuilder<ExpenseItem> builder)
    {
        builder.ToTable("expense_items", "snapsplit");

        builder.HasKey(ei => ei.Id);

        builder.Property(ei => ei.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ei => ei.Amount)
            .HasPrecision(18, 2);

        builder.HasOne(ei => ei.Expense)
            .WithMany(e => e.Items)
            .HasForeignKey(ei => ei.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ei => ei.PaidBy)
            .WithMany()
            .HasForeignKey(ei => ei.PaidById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
