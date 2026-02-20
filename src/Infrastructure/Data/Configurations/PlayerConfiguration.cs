using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        // Player - Team
        builder.HasOne(p => p.Team)
            .WithMany(t => t.Players)
            .HasForeignKey(p => p.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player - User (Optional)
        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Soft Delete
        builder.Property<bool>("IsDeleted");
        builder.HasQueryFilter(p => !EF.Property<bool>(p, "IsDeleted"));

        // Indexes
        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("IX_Players_User");

        builder.HasIndex(p => new { p.TeamId, p.UserId })
            .IsUnique()
            .HasDatabaseName("UQ_Player_Team_User");

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_Players_Status");

        builder.HasIndex(p => p.Position)
            .HasDatabaseName("IX_Players_Position");
    }
}
