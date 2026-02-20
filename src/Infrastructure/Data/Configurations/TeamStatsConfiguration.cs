using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TeamStatsConfiguration : IEntityTypeConfiguration<TeamStats>
{
    public void Configure(EntityTypeBuilder<TeamStats> builder)
    {
        // Team - TeamStats (1-to-1)
        // PROD-FIX: Configure as optional to avoid EF Core warning about
        // required relationship + query filter mismatch
        builder.HasOne(s => s.Team)
            .WithOne(t => t.Statistics)
            .HasForeignKey<TeamStats>(s => s.TeamId)
            .IsRequired(false);

        // Soft Delete — aligned with Team's query filter
        // PROD-FIX: Eliminates EF Core warning about required relationship
        // where principal (Team) has a query filter but dependent (TeamStats) doesn't.
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(s => !EF.Property<bool>(s, "IsDeleted"));

        // Unique index
        builder.HasIndex(s => s.TeamId)
            .IsUnique()
            .HasDatabaseName("IX_TeamStats_TeamId");
    }
}
