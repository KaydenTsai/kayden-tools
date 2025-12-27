using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("settlements", "snapsplit");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Amount)
            .HasPrecision(18, 2);

        builder.HasOne(s => s.Bill)
            .WithMany(b => b.Settlements)
            .HasForeignKey(s => s.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.FromMember)
            .WithMany()
            .HasForeignKey(s => s.FromMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ToMember)
            .WithMany()
            .HasForeignKey(s => s.ToMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.BillId, s.FromMemberId, s.ToMemberId })
            .IsUnique();
    }
}
