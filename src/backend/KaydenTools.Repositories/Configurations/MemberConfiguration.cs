using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members", "snapsplit");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(m => m.Bill)
            .WithMany(b => b.Members)
            .HasForeignKey(m => m.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.BillId, m.DisplayOrder });
    }
}
