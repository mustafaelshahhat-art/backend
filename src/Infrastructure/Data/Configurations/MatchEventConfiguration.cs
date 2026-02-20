using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        // MatchEvent - Match
        builder.HasOne(me => me.Match)
            .WithMany(m => m.Events)
            .HasForeignKey(me => me.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft Delete
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(me => !EF.Property<bool>(me, "IsDeleted"));

        // Indexes
        builder.HasIndex(me => me.MatchId)
            .HasDatabaseName("IX_MatchEvents_MatchId");
    }
}
