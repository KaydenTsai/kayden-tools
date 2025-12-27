using KaydenTools.Models.SnapSplit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaydenTools.Repositories.Configurations;

public class ExpenseParticipantConfiguration : IEntityTypeConfiguration<ExpenseParticipant>
{
    public void Configure(EntityTypeBuilder<ExpenseParticipant> builder)
    {
        builder.ToTable("expense_participants", "snapsplit");

        builder.HasKey(ep => new { ep.ExpenseId, ep.MemberId });

        builder.HasOne(ep => ep.Expense)
            .WithMany(e => e.Participants)
            .HasForeignKey(ep => ep.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ep => ep.Member)
            .WithMany()
            .HasForeignKey(ep => ep.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
