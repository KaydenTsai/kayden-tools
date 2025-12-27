using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
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

        builder.HasOne(e => e.Bill)
            .WithMany(b => b.Expenses)
            .HasForeignKey(e => e.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.PaidBy)
            .WithMany()
            .HasForeignKey(e => e.PaidById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
