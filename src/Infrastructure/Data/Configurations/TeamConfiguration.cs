using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        // Soft Delete
        builder.Property<bool>("IsDeleted");
        builder.HasQueryFilter(t => !EF.Property<bool>(t, "IsDeleted"));

        // SCALE PROTECTION INDEXES
        builder.HasIndex(t => t.IsActive)
            .HasDatabaseName("IX_Teams_IsActive");

        builder.HasIndex(t => t.City)
            .HasDatabaseName("IX_Teams_City");

        // PERF-FIX: Name index for ORDER BY in GetTeamsPaged
        builder.HasIndex(t => t.Name)
            .HasDatabaseName("IX_Teams_Name");
    }
}
