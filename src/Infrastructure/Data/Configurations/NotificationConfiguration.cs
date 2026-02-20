using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        // Notification - User (Cascade delete when User is hard deleted)
        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Enum → int storage
        builder.Property(n => n.Type)
            .HasConversion<int>();
        builder.Property(n => n.Category)
            .HasConversion<int>();
        builder.Property(n => n.Priority)
            .HasConversion<int>();

        builder.Property(n => n.EntityType).HasMaxLength(50);
        builder.Property(n => n.ActionUrl).HasMaxLength(500);

        // Indexes
        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_User_Read");

        // Category filter
        builder.HasIndex(n => new { n.UserId, n.Category, n.CreatedAt })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Notifications_User_Category_CreatedAt");

        // Expiry cleanup
        builder.HasIndex(n => n.ExpiresAt)
            .HasFilter("[ExpiresAt] IS NOT NULL")
            .HasDatabaseName("IX_Notifications_ExpiresAt");

        // User inbox ordered by date (covers GetByUserIdAsync hot path)
        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notifications_User_CreatedAt");
    }
}
