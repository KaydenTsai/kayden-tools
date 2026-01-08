using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    #region IEntityTypeConfiguration<Member> Members

    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members", "snapsplit");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(m => m.OriginalName)
            .HasMaxLength(50);

        builder.Property(m => m.LocalClientId)
            .HasMaxLength(100);

        builder.HasOne(m => m.Bill)
            .WithMany(b => b.Members)
            .HasForeignKey(m => m.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.LinkedUser)
            .WithMany()
            .HasForeignKey(m => m.LinkedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => new { m.BillId, m.DisplayOrder });
        builder.HasIndex(m => m.LinkedUserId);

        // 冪等性唯一索引：同一帳單內 LocalClientId 不能重複（僅當 LocalClientId 不為 null 時）
        builder.HasIndex(m => new { m.BillId, m.LocalClientId })
            .HasFilter("\"LocalClientId\" IS NOT NULL")
            .IsUnique();

        // Soft Delete
        builder.Property(m => m.IsDeleted)
            .HasDefaultValue(false);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }

    #endregion
}
