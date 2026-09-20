using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

/// <summary>Bus persist type; SQL table remains Vehicles until a rename migration (specs/buses.md).</summary>
internal sealed class BusConfiguration : IEntityTypeConfiguration<Bus>
{
    public void Configure(EntityTypeBuilder<Bus> entity)
    {
        entity.ToTable("Vehicles");
        entity.HasKey(e => e.BusId);
        entity.Property(e => e.BusId).HasColumnName("VehicleId");

        entity.Property(e => e.BusNumber).IsRequired().HasMaxLength(20);
        entity.Property(e => e.VINNumber).IsRequired().HasMaxLength(17).HasColumnName("VIN");
        entity.Property(e => e.LicenseNumber).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Make).IsRequired().HasMaxLength(50).HasDefaultValue("Unknown");
        entity.Property(e => e.Model).IsRequired().HasMaxLength(50).HasDefaultValue("Unknown");
        entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Active");
        entity.Property(e => e.FleetType).HasMaxLength(20);
        entity.Property(e => e.FuelType).HasMaxLength(20);
        entity.Property(e => e.Department).HasMaxLength(50);
        entity.Property(e => e.GPSDeviceId).HasMaxLength(100);
        entity.Property(e => e.PurchasePrice).HasColumnType("decimal(10,2)");
        entity.Property(e => e.FuelCapacity).HasColumnType("decimal(8,2)");
        entity.Property(e => e.MilesPerGallon).HasColumnType("decimal(6,2)");
        entity.Property(e => e.InsurancePolicyNumber).HasMaxLength(100);
        entity.Property(e => e.SpecialEquipment).HasMaxLength(1000);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.CurrentLatitude).HasColumnType("decimal(10,8)");
        entity.Property(e => e.CurrentLongitude).HasColumnType("decimal(11,8)");

        entity.HasIndex(e => e.BusNumber).IsUnique().HasDatabaseName("IX_Vehicles_BusNumber");
        entity.HasIndex(e => e.VINNumber).IsUnique().HasDatabaseName("IX_Vehicles_VINNumber");
        entity.HasIndex(e => e.LicenseNumber).IsUnique().HasDatabaseName("IX_Vehicles_LicenseNumber");
        entity.HasIndex(e => e.Status).HasDatabaseName("IX_Vehicles_Status");
        entity.HasIndex(e => e.DateLastInspection).HasDatabaseName("IX_Vehicles_DateLastInspection");
        entity.HasIndex(e => e.InsuranceExpiryDate).HasDatabaseName("IX_Vehicles_InsuranceExpiryDate");
        entity.HasIndex(e => e.FleetType).HasDatabaseName("IX_Vehicles_FleetType");
        entity.HasIndex(e => new { e.Make, e.Model, e.Year }).HasDatabaseName("IX_Vehicles_MakeModelYear");
        entity.HasIndex(e => new { e.CurrentLatitude, e.CurrentLongitude }).HasDatabaseName("IX_Vehicles_CurrentLocation");
    }
}

internal sealed class FuelConfiguration : IEntityTypeConfiguration<Fuel>
{
    public void Configure(EntityTypeBuilder<Fuel> entity)
    {
        entity.ToTable("Fuel");
        entity.HasKey(e => e.FuelId);
        entity.Property(e => e.FuelLocation).HasMaxLength(FuelConstraints.MaxLocationLength);
        entity.Property(e => e.FuelType).HasMaxLength(FuelConstraints.MaxFuelTypeLength).HasDefaultValue("Gasoline");
        entity.Property(e => e.Notes).HasMaxLength(FuelConstraints.MaxNotesLength);
        entity.Property(e => e.Gallons).HasColumnType("decimal(8,3)");
        entity.Property(e => e.PricePerGallon).HasColumnType("decimal(8,3)");
        entity.Property(e => e.TotalCost).HasColumnType("decimal(10,2)");

        entity.HasOne(f => f.Vehicle)
            .WithMany(v => v.FuelRecords)
            .HasForeignKey(f => f.VehicleFueledId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Fuel_Vehicle");

        entity.HasIndex(e => e.FuelDate).HasDatabaseName("IX_Fuel_FuelDate");
        entity.HasIndex(e => e.VehicleFueledId).HasDatabaseName("IX_Fuel_VehicleId");
        entity.HasIndex(e => new { e.VehicleFueledId, e.FuelDate }).HasDatabaseName("IX_Fuel_VehicleDate");
        entity.HasIndex(e => e.FuelLocation).HasDatabaseName("IX_Fuel_Location");
        entity.HasIndex(e => e.FuelType).HasDatabaseName("IX_Fuel_Type");
    }
}

internal sealed class MaintenanceConfiguration : IEntityTypeConfiguration<Maintenance>
{
    public void Configure(EntityTypeBuilder<Maintenance> entity)
    {
        entity.ToTable("Maintenance");
        entity.HasKey(e => e.MaintenanceId);
        entity.Property(e => e.MaintenanceCompleted).HasMaxLength(100);
        entity.Property(e => e.Vendor).HasMaxLength(100);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("Normal");
        entity.Property(e => e.RepairCost).HasColumnType("decimal(10,2)");
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasOne(m => m.Vehicle)
            .WithMany(v => v.MaintenanceRecords)
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Maintenance_Vehicle");

        entity.HasIndex(e => e.Date).HasDatabaseName("IX_Maintenance_Date");
        entity.HasIndex(e => e.VehicleId).HasDatabaseName("IX_Maintenance_VehicleId");
        entity.HasIndex(e => e.MaintenanceCompleted).HasDatabaseName("IX_Maintenance_Type");
        entity.HasIndex(e => new { e.VehicleId, e.Date }).HasDatabaseName("IX_Maintenance_VehicleDate");
        entity.HasIndex(e => e.Priority).HasDatabaseName("IX_Maintenance_Priority");
    }
}
