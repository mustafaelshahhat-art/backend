using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        // Match - Teams
        builder.HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Match - Tournament
        builder.HasOne(m => m.Tournament)
            .WithMany(t => t.Matches)
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft Delete
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(m => !EF.Property<bool>(m, "IsDeleted"));

        // Indexes
        builder.HasIndex(m => m.Date)
            .HasDatabaseName("IX_Matches_Date");

        builder.HasIndex(m => m.Status)
            .HasDatabaseName("IX_Matches_Status");

        builder.HasIndex(m => new { m.TournamentId, m.Status })
            .HasDatabaseName("IX_Matches_Tournament_Status");

        builder.HasIndex(m => m.HomeTeamId)
            .HasDatabaseName("IX_Matches_HomeTeamId");

        builder.HasIndex(m => m.AwayTeamId)
            .HasDatabaseName("IX_Matches_AwayTeamId");

        builder.HasIndex(m => new { m.TournamentId, m.GroupId })
            .HasDatabaseName("IX_Matches_Tournament_Group");

        builder.HasIndex(m => new { m.TournamentId, m.RoundNumber })
            .HasDatabaseName("IX_Matches_Tournament_Round");

        // Covering index for listing (tournament + status + date)
        builder.HasIndex(m => new { m.TournamentId, m.Status, m.Date })
            .HasDatabaseName("IX_Matches_Tournament_Status_Date");
    }
}
