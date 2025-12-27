using KaydenTools.Models.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "public");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .HasMaxLength(255);

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.LineUserId)
            .HasMaxLength(100);

        builder.Property(u => u.GoogleUserId)
            .HasMaxLength(100);

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("email IS NOT NULL AND is_deleted = false");

        builder.HasIndex(u => u.LineUserId)
            .IsUnique()
            .HasFilter("line_user_id IS NOT NULL AND is_deleted = false");

        builder.HasIndex(u => u.GoogleUserId)
            .IsUnique()
            .HasFilter("google_user_id IS NOT NULL AND is_deleted = false");

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
