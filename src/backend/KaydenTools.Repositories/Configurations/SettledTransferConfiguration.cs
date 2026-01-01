using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class SettledTransferConfiguration : IEntityTypeConfiguration<SettledTransfer>
{
    public void Configure(EntityTypeBuilder<SettledTransfer> builder)
    {
        builder.ToTable("settled_transfers", "snapsplit");

        // 複合主鍵
        builder.HasKey(s => new { s.BillId, s.FromMemberId, s.ToMemberId });

        builder.Property(s => s.Amount)
            .HasPrecision(12, 2);

        builder.HasOne(s => s.Bill)
            .WithMany(b => b.SettledTransfers)
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

        builder.HasIndex(s => s.BillId);
    }
}
