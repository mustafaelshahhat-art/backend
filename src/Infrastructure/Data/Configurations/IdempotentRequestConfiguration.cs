using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class IdempotentRequestConfiguration : IEntityTypeConfiguration<IdempotentRequest>
{
    public void Configure(EntityTypeBuilder<IdempotentRequest> builder)
    {
        builder.HasIndex(r => new { r.Key, r.Route })
            .IsUnique()
            .HasDatabaseName("UQ_IdempotentRequests_Key_Route");
    }
}
