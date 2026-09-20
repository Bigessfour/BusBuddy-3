using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> entity)
    {
        entity.ToTable("Drivers");
        entity.HasKey(e => e.DriverId);
        entity.Property(e => e.DriverId).HasColumnName("DriverID");
        entity.Property(e => e.DriverName).IsRequired().HasMaxLength(100).HasDefaultValue("Unknown Driver");
        entity.Property(e => e.DriverPhone).HasMaxLength(20);
        entity.Property(e => e.DriverEmail).HasMaxLength(100);
        entity.Property(e => e.DriversLicenceType).HasMaxLength(20).HasColumnName("DriversLicenseType").HasDefaultValue("Standard");
        entity.Property(e => e.Status).HasDefaultValue("Active").ValueGeneratedNever();
        entity.Property(e => e.Address).HasMaxLength(200);
        entity.Property(e => e.City).HasMaxLength(50);
        entity.Property(e => e.State).HasMaxLength(20);
        entity.Property(e => e.Zip).HasMaxLength(10);
        entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
        entity.Property(e => e.EmergencyContactPhone).HasMaxLength(20);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.HomeLatitude).HasColumnType("decimal(10,8)");
        entity.Property(e => e.HomeLongitude).HasColumnType("decimal(11,8)");
        entity.Property(e => e.EmployingDistrict).HasMaxLength(150);
        entity.Property(e => e.DutyCategory).HasMaxLength(20);
        entity.Property(e => e.VehicleCategory).HasMaxLength(60);
        entity.Property(e => e.CdlRestrictions).HasMaxLength(50);
        entity.Property(e => e.MedicalFormType).HasMaxLength(40);

        entity.HasMany(e => e.TrainingRecords)
            .WithOne(t => t.Driver)
            .HasForeignKey(t => t.DriverId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.DriverEmail).HasDatabaseName("IX_Drivers_Email");
        entity.HasIndex(e => e.DriverPhone).HasDatabaseName("IX_Drivers_Phone");
        entity.HasIndex(e => e.DriversLicenceType).HasDatabaseName("IX_Drivers_LicenseType");
        entity.HasIndex(e => e.LicenseExpiryDate).HasDatabaseName("IX_Drivers_LicenseExpiration");
        entity.HasIndex(e => e.TrainingComplete).HasDatabaseName("IX_Drivers_TrainingComplete");
        entity.HasIndex(e => new { e.HomeLatitude, e.HomeLongitude }).HasDatabaseName("IX_Drivers_HomeLocation");
    }
}

internal sealed class DriverTrainingRecordConfiguration : IEntityTypeConfiguration<DriverTrainingRecord>
{
    public void Configure(EntityTypeBuilder<DriverTrainingRecord> entity)
    {
        entity.ToTable("DriverTrainingRecords");
        entity.HasKey(e => e.TrainingRecordId);
        entity.Property(e => e.RequirementCode).IsRequired().HasMaxLength(80);
        entity.Property(e => e.RequirementName).IsRequired().HasMaxLength(200);
        entity.Property(e => e.CertificateOrReference).HasMaxLength(100);
        entity.Property(e => e.ProviderOrInstructor).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_DriverTrainingRecords_Driver");
        entity.HasIndex(e => new { e.DriverId, e.RequirementCode })
            .IsUnique()
            .HasDatabaseName("IX_DriverTrainingRecords_Driver_Code");
    }
}
