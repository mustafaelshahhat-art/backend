using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class MatchMessageConfiguration : IEntityTypeConfiguration<MatchMessage>
{
    public void Configure(EntityTypeBuilder<MatchMessage> builder)
    {
        // MatchMessage - Match
        builder.HasOne(mm => mm.Match)
            .WithMany()
            .HasForeignKey(mm => mm.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft Delete
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(mm => !EF.Property<bool>(mm, "IsDeleted"));

        // Indexes
        builder.HasIndex(mm => mm.MatchId)
            .HasDatabaseName("IX_MatchMessages_MatchId");

        // Chat history ordered by timestamp
        builder.HasIndex(mm => new { mm.MatchId, mm.Timestamp })
            .HasDatabaseName("IX_MatchMessages_Match_Timestamp");
    }
}
