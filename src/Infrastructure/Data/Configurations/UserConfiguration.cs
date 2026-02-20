using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("UQ_Users_Email");

        // Legacy composite index on deprecated text columns — kept for migration period
#pragma warning disable CS0618
        builder.HasIndex(u => new { u.Governorate, u.City, u.Neighborhood })
            .HasDatabaseName("IX_Users_Location");
#pragma warning restore CS0618

        // User → Location FKs
        builder.HasOne(u => u.GovernorateNav)
            .WithMany(g => g.Users)
            .HasForeignKey(u => u.GovernorateId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(u => u.CityNav)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CityId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(u => u.AreaNav)
            .WithMany(a => a.Users)
            .HasForeignKey(u => u.AreaId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(u => u.GovernorateId).HasDatabaseName("IX_User_GovernorateId");
        builder.HasIndex(u => u.CityId).HasDatabaseName("IX_User_CityId");
        builder.HasIndex(u => u.AreaId).HasDatabaseName("IX_User_AreaId");

        builder.HasIndex(u => u.CreatedAt)
            .HasDatabaseName("IX_Users_CreatedAt");

        // User: refresh token lookup (filtered — only non-null)
        builder.HasIndex(u => u.RefreshToken)
            .HasFilter("[RefreshToken] IS NOT NULL")
            .HasDatabaseName("IX_Users_RefreshToken_Filtered");
    }
}
