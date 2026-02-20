using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TeamJoinRequestConfiguration : IEntityTypeConfiguration<TeamJoinRequest>
{
    public void Configure(EntityTypeBuilder<TeamJoinRequest> builder)
    {
        // Soft Delete
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(r => !EF.Property<bool>(r, "IsDeleted"));

        builder.Property(r => r.Status).HasMaxLength(50);

        // Indexes
        builder.HasIndex(r => new { r.TeamId, r.Status })
            .HasDatabaseName("IX_TeamJoinRequests_Team_Status");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_TeamJoinRequests_UserId");

        // User + status (covers "my pending requests")
        builder.HasIndex(r => new { r.UserId, r.Status })
            .HasDatabaseName("IX_TeamJoinRequests_User_Status");
    }
}
