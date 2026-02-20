using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
{
    public void Configure(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("Governorates");
        builder.Property(g => g.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(g => g.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(g => g.IsActive).HasDefaultValue(true);
        builder.Property(g => g.SortOrder).HasDefaultValue(0);
        builder.HasIndex(g => g.NameAr).IsUnique().HasDatabaseName("UQ_Governorates_NameAr");
        builder.HasIndex(g => g.NameEn).IsUnique().HasDatabaseName("UQ_Governorates_NameEn");
        builder.HasIndex(g => g.IsActive).HasDatabaseName("IX_Governorates_IsActive");
    }
}

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.Property(c => c.SortOrder).HasDefaultValue(0);
        builder.HasOne(c => c.Governorate)
            .WithMany(g => g.Cities)
            .HasForeignKey(c => c.GovernorateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.GovernorateId).HasDatabaseName("IX_City_GovernorateId");
        builder.HasIndex(c => new { c.GovernorateId, c.NameAr }).IsUnique().HasDatabaseName("UQ_City_Governorate_NameAr");
        builder.HasIndex(c => c.IsActive).HasDatabaseName("IX_Cities_IsActive");
    }
}

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");
        builder.Property(a => a.NameAr).IsRequired().HasMaxLength(100);
        builder.Property(a => a.NameEn).IsRequired().HasMaxLength(100);
        builder.Property(a => a.IsActive).HasDefaultValue(true);
        builder.Property(a => a.SortOrder).HasDefaultValue(0);
        builder.HasOne(a => a.City)
            .WithMany(c => c.Areas)
            .HasForeignKey(a => a.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CityId).HasDatabaseName("IX_Area_CityId");
        builder.HasIndex(a => new { a.CityId, a.NameAr }).IsUnique().HasDatabaseName("UQ_Area_City_NameAr");
        builder.HasIndex(a => a.IsActive).HasDatabaseName("IX_Areas_IsActive");
    }
}
