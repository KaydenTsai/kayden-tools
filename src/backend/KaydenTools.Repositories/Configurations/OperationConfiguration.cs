using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("operations", "snapsplit");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version)
            .IsRequired();

        builder.Property(x => x.OpType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ClientId)
            .IsRequired()
            .HasMaxLength(100);

        // Payload is JSONB in Postgres
        builder.Property(x => x.Payload)
            .HasColumnType("jsonb");

        // Unique constraint on (BillId, Version) to ensure sequential consistency
        builder.HasIndex(x => new { x.BillId, x.Version })
            .IsUnique();

        builder.HasOne(x => x.Bill)
            .WithMany(x => x.Operations)
            .HasForeignKey(x => x.BillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
