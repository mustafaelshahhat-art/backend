using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TournamentPlayerConfiguration : IEntityTypeConfiguration<TournamentPlayer>
{
    public void Configure(EntityTypeBuilder<TournamentPlayer> builder)
    {
        // TournamentPlayer - Tournament
        builder.HasOne(tp => tp.Tournament)
            .WithMany(t => t.TournamentPlayers)
            .HasForeignKey(tp => tp.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // TournamentPlayer - Registration
        builder.HasOne(tp => tp.Registration)
            .WithMany()
            .HasForeignKey(tp => tp.RegistrationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index
        builder.HasIndex(tp => new { tp.TournamentId, tp.PlayerId })
            .IsUnique()
            .HasDatabaseName("UQ_TournamentPlayer_Tournament_Player");

        // Soft Delete
        builder.Property<bool>("IsDeleted").HasDefaultValue(false);
        builder.HasQueryFilter(tp => !EF.Property<bool>(tp, "IsDeleted"));

        // Indexes
        builder.HasIndex(tp => tp.RegistrationId)
            .HasDatabaseName("IX_TournamentPlayers_RegistrationId");
    }
}
