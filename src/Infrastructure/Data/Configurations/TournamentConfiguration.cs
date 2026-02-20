using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        // Ignore computed alias properties
        builder.Ignore(t => t.OpeningMatchHomeTeamId);
        builder.Ignore(t => t.OpeningMatchAwayTeamId);

        // Tournament - Creator User
        builder.HasOne(t => t.CreatorUser)
            .WithMany()
            .HasForeignKey(t => t.CreatorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Precision
        builder.Property(t => t.EntryFee).HasPrecision(18, 2);

        // PROD-FIX: Disable Output Clause for SQL Server triggers compatibility
        builder.ToTable(tb => tb.UseSqlOutputClause(false));

        // Indexes
        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("UQ_Tournaments_Name");

        builder.HasIndex(t => new { t.CreatorUserId, t.Status })
            .HasDatabaseName("IX_Tournaments_Creator_Status");

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_Tournaments_Status");

        builder.HasIndex(t => t.StartDate)
            .HasDatabaseName("IX_Tournaments_StartDate");

        builder.HasIndex(t => t.CreatedAt)
            .HasDatabaseName("IX_Tournaments_CreatedAt");
    }
}
