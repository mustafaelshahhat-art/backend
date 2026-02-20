using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        // Activity - User
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enhanced column config
        builder.Property(a => a.Severity)
            .HasConversion<int>()
            .HasDefaultValue(ActivitySeverity.Info);
        builder.Property(a => a.ActorRole).HasMaxLength(50);
        builder.Property(a => a.EntityType).HasMaxLength(50);
        builder.Property(a => a.EntityName).HasMaxLength(200);

        // Indexes
        builder.HasIndex(a => new { a.UserId, a.CreatedAt })
            .HasDatabaseName("IX_Activities_User_Date");

        builder.HasIndex(a => a.Type)
            .HasDatabaseName("IX_Activities_Type");

        builder.HasIndex(a => a.CreatedAt)
            .IsDescending()
            .HasDatabaseName("IX_Activities_CreatedAt_Desc");

        builder.HasIndex(a => new { a.Severity, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Activities_Severity_CreatedAt");

        builder.HasIndex(a => new { a.EntityType, a.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Activities_EntityType_CreatedAt");
    }
}
