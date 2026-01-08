using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    #region IEntityTypeConfiguration<Bill> Members

    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("bills", "snapsplit");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.LocalClientId)
            .HasMaxLength(50);

        builder.Property(b => b.ShareCode)
            .HasMaxLength(20);

        // 注意：暫時移除 IsConcurrencyToken 以避免與應用層級版本檢查的衝突
        // 應用層級已在 SyncBillAsync 中實作樂觀鎖檢查
        // builder.Property(b => b.Version)
        //     .IsConcurrencyToken();

        builder.HasIndex(b => b.ShareCode)
            .IsUnique()
            .HasFilter("share_code IS NOT NULL AND is_deleted = false");

        builder.HasIndex(b => b.OwnerId);

        // 冪等性索引：防止同一用戶重複建立相同本地 ID 的帳單
        builder.HasIndex(b => new { b.LocalClientId, b.OwnerId })
            .IsUnique()
            .HasFilter("local_client_id IS NOT NULL AND owner_id IS NOT NULL AND is_deleted = false");

        builder.HasQueryFilter(b => !b.IsDeleted);
    }

    #endregion
}
