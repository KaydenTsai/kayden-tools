using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ExpenseItemParticipantConfiguration : IEntityTypeConfiguration<ExpenseItemParticipant>
{
    public void Configure(EntityTypeBuilder<ExpenseItemParticipant> builder)
    {
        builder.ToTable("expense_item_participants", "snapsplit");

        builder.HasKey(eip => new { eip.ExpenseItemId, eip.MemberId });

        builder.HasOne(eip => eip.ExpenseItem)
            .WithMany(ei => ei.Participants)
            .HasForeignKey(eip => eip.ExpenseItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(eip => eip.Member)
            .WithMany()
            .HasForeignKey(eip => eip.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
