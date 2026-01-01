using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills", "snapsplit");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.ShareCode)
            .HasMaxLength(20);

        // 將 Version 配置為並發令牌，確保資料庫層級的樂觀鎖
        builder.Property(b => b.Version)
            .IsConcurrencyToken();

        builder.HasIndex(b => b.ShareCode)
            .IsUnique()
            .HasFilter("share_code IS NOT NULL AND is_deleted = false");

        builder.HasIndex(b => b.OwnerId);

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
