using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TeamRegistrationConfiguration : IEntityTypeConfiguration<TeamRegistration>
{
    public void Configure(EntityTypeBuilder<TeamRegistration> builder)
    {
        // Tournament - Registrations
        builder.HasOne(tr => tr.Tournament)
            .WithMany(t => t.Registrations)
            .HasForeignKey(tr => tr.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // TeamRegistration - Team
        builder.HasOne(tr => tr.Team)
            .WithMany(t => t.Registrations)
            .HasForeignKey(tr => tr.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft Delete
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(tr => !EF.Property<bool>(tr, "IsDeleted"));

        // Unique constraint
        builder.HasIndex(tr => new { tr.TournamentId, tr.TeamId })
            .IsUnique()
            .HasDatabaseName("UQ_TeamRegistration_Tournament_Team");

        // Indexes
        builder.HasIndex(tr => tr.Status)
            .HasDatabaseName("IX_TeamRegistration_Status");

        builder.HasIndex(tr => tr.CreatedAt)
            .HasDatabaseName("IX_TeamRegistration_CreatedAt");

        // Tournament + status (covers registration approval queries)
        builder.HasIndex(tr => new { tr.TournamentId, tr.Status })
            .HasDatabaseName("IX_TeamRegistration_Tournament_Status");

        // Knockout qualification flag — set by ConfirmManualQualificationCommandHandler
        builder.Property(tr => tr.IsQualifiedForKnockout)
            .HasDefaultValue(false)
            .IsRequired();
    }
}
