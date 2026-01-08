using KaydenTools.Models.UrlShortener.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class UrlClickConfiguration : IEntityTypeConfiguration<UrlClick>
{
    #region IEntityTypeConfiguration<UrlClick> Members

    public void Configure(EntityTypeBuilder<UrlClick> builder)
    {
        builder.ToTable("url_clicks", "urlshortener");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClickedAt)
            .IsRequired();

        builder.Property(c => c.IpAddress)
            .HasMaxLength(45);

        builder.Property(c => c.UserAgent)
            .HasMaxLength(512);

        builder.Property(c => c.Referrer)
            .HasMaxLength(2048);

        builder.Property(c => c.DeviceType)
            .HasMaxLength(20);

        // Indexes for efficient querying
        builder.HasIndex(c => c.ShortUrlId);
        builder.HasIndex(c => c.ClickedAt);

        // Composite index for stats queries
        builder.HasIndex(c => new { c.ShortUrlId, c.ClickedAt });
    }

    #endregion
}
