using KaydenTools.Models.UrlShortener.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ShortUrlConfiguration : IEntityTypeConfiguration<ShortUrl>
{
    #region IEntityTypeConfiguration<ShortUrl> Members

    public void Configure(EntityTypeBuilder<ShortUrl> builder)
    {
        builder.ToTable("short_urls", "urlshortener");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.OriginalUrl)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(s => s.ShortCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.ClickCount)
            .HasDefaultValue(0);

        builder.Property(s => s.IsActive)
            .HasDefaultValue(true);

        // Unique index on ShortCode (excluding soft-deleted)
        builder.HasIndex(s => s.ShortCode)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(s => s.OwnerId);
        builder.HasIndex(s => s.ExpiresAt);

        // Soft delete query filter
        builder.HasQueryFilter(s => !s.IsDeleted);

        // Relationship with UrlClick
        builder.HasMany(s => s.Clicks)
            .WithOne(c => c.ShortUrl)
            .HasForeignKey(c => c.ShortUrlId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    #endregion
}
