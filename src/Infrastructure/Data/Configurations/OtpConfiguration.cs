using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class OtpConfiguration : IEntityTypeConfiguration<Otp>
{
    public void Configure(EntityTypeBuilder<Otp> builder)
    {
        // Otp - User
        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Otp: User is hard-deleted with cascade, no IsDeleted filter needed

        // Indexes: lookup by userId + type + active status
        builder.HasIndex(o => new { o.UserId, o.Type, o.IsUsed })
            .HasDatabaseName("IX_Otps_User_Type_IsUsed");
    }
}
