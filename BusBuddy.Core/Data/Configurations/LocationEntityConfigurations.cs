using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

internal sealed class DestinationConfiguration : IEntityTypeConfiguration<Destination>
{
    public void Configure(EntityTypeBuilder<Destination> entity)
    {
        entity.ToTable("Destinations");
        entity.HasKey(e => e.DestinationId);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Address).IsRequired().HasMaxLength(300);
        entity.Property(e => e.City).IsRequired().HasMaxLength(100);
        entity.Property(e => e.State).IsRequired().HasMaxLength(2);
        entity.Property(e => e.ZipCode).IsRequired().HasMaxLength(10);
        entity.Property(e => e.DestinationType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.DistrictName).HasMaxLength(150);
        entity.Property(e => e.GradeMin).HasMaxLength(20);
        entity.Property(e => e.GradeMax).HasMaxLength(20);
        entity.Property(e => e.AdminContactName).HasMaxLength(100);
        entity.Property(e => e.AdminPhone).HasMaxLength(20);
        entity.Property(e => e.AdminEmail).HasMaxLength(100);
        entity.Property(e => e.StartTime);
        entity.Property(e => e.DismissalTime);
        entity.HasIndex(e => e.DestinationType).HasDatabaseName("IX_Destinations_Type");
        entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_Destinations_Active");
    }
}

internal sealed class PickupStopConfiguration : IEntityTypeConfiguration<PickupStop>
{
    public void Configure(EntityTypeBuilder<PickupStop> entity)
    {
        entity.ToTable("PickupStops");
        entity.HasKey(e => e.PickupStopId);
        entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Address).HasMaxLength(300);
        entity.Property(e => e.StopType).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.Active).HasDefaultValue(true);
        entity.HasIndex(e => e.Active).HasDatabaseName("IX_PickupStops_Active");
    }
}
