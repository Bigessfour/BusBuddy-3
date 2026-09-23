using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> entity)
    {
        entity.ToTable("Students");
        entity.HasKey(e => e.StudentId);
        entity.Property(e => e.StudentName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Grade).HasMaxLength(20);
        entity.Property(e => e.MedicalNotes).HasMaxLength(1000);
        entity.Property(e => e.TransportationNotes).HasMaxLength(1000);
        entity.Property(e => e.EmergencyPhone).HasMaxLength(20);
        entity.Property(e => e.School).HasMaxLength(100);
        entity.Property(e => e.ParentGuardian).HasMaxLength(100);
        entity.Property(e => e.ParentEmail).HasMaxLength(100);
        entity.Property(e => e.CellPhone).HasMaxLength(20);
        entity.Property(e => e.EmergencyContactName).HasMaxLength(100);
        entity.Property(e => e.HomeAddress).HasMaxLength(200);
        entity.Property(e => e.City).HasMaxLength(50);
        entity.Property(e => e.State).HasMaxLength(2);
        entity.Property(e => e.Zip).HasMaxLength(10);
        entity.Property(e => e.PlaceId).HasMaxLength(256);
        entity.Property(e => e.HomePickupClerkAdjusted).HasDefaultValue(false);
        entity.Property(e => e.SchoolYear).HasMaxLength(9);
        entity.Property(e => e.HasMedicalNeeds).HasDefaultValue(false);
        entity.Property(e => e.RequiresSpecialNeedsBus).HasDefaultValue(false);
        entity.Property(e => e.RequiresWheelchair).HasDefaultValue(false);
        entity.Property(e => e.RequiresSeatBelt).HasDefaultValue(false);
        entity.Property(e => e.RequiresAide).HasDefaultValue(false);
        entity.Property(e => e.RidesAm).HasDefaultValue(false);
        entity.Property(e => e.RidesPm).HasDefaultValue(false);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(e => e.StudentName).HasDatabaseName("IX_Students_Name");
        entity.HasIndex(e => e.Grade).HasDatabaseName("IX_Students_Grade");
        entity.HasIndex(e => e.School).HasDatabaseName("IX_Students_School");
        entity.HasIndex(e => e.Active).HasDatabaseName("IX_Students_Active");
        entity.HasIndex(e => e.AMRoute).HasDatabaseName("IX_Students_AMRoute");
        entity.HasIndex(e => e.PMRoute).HasDatabaseName("IX_Students_PMRoute");
        entity.HasIndex(e => e.SchoolYear).HasDatabaseName("IX_Students_SchoolYear");
        entity.HasIndex(e => e.AmRouteId).HasDatabaseName("IX_Students_AmRouteId");
        entity.HasIndex(e => e.PmRouteId).HasDatabaseName("IX_Students_PmRouteId");

        entity.HasOne<Route>()
            .WithMany()
            .HasForeignKey(e => e.AmRouteId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne<Route>()
            .WithMany()
            .HasForeignKey(e => e.PmRouteId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.Destination)
            .WithMany()
            .HasForeignKey(e => e.DestinationId)
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(e => e.PickupStop)
            .WithMany()
            .HasForeignKey(e => e.PickupStopId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class StudentDeletionLogConfiguration : IEntityTypeConfiguration<StudentDeletionLog>
{
    public void Configure(EntityTypeBuilder<StudentDeletionLog> entity)
    {
        entity.ToTable("StudentDeletionLogs");
        entity.HasKey(e => e.StudentDeletionLogId);
        entity.Property(e => e.Reason).IsRequired().HasMaxLength(32);
        entity.Property(e => e.Notes).HasMaxLength(200);
        entity.Property(e => e.StudentNumber).HasMaxLength(20);
        entity.Property(e => e.SchoolYear).HasMaxLength(9);
        entity.HasIndex(e => e.StudentId).HasDatabaseName("IX_StudentDeletionLogs_StudentId");
        entity.HasIndex(e => e.DeletedUtc).HasDatabaseName("IX_StudentDeletionLogs_DeletedUtc");
    }
}

internal sealed class FamilyConfiguration : IEntityTypeConfiguration<Family>
{
    public void Configure(EntityTypeBuilder<Family> entity)
    {
        entity.ToTable("Families");
        entity.HasKey(e => e.FamilyId);
        entity.Property(e => e.ParentGuardian).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Address).IsRequired().HasMaxLength(200);
        entity.Property(e => e.City).IsRequired().HasMaxLength(50);
        entity.Property(e => e.County).HasMaxLength(50);
        entity.Property(e => e.HomePhone).HasMaxLength(20);
        entity.Property(e => e.CellPhone).HasMaxLength(20);
        entity.Property(e => e.EmergencyContact).HasMaxLength(100);
        entity.Property(e => e.JointParent).HasMaxLength(100);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasMany(f => f.Guardians)
            .WithOne(g => g.Family)
            .HasForeignKey(g => g.FamilyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Guardians_Family");
        entity.HasMany(f => f.Students)
            .WithOne(s => s.Family)
            .HasForeignKey(s => s.FamilyId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Students_Family");

        entity.HasIndex(e => new { e.ParentGuardian, e.Address }).HasDatabaseName("IX_Families_ParentAddress");
        entity.HasIndex(e => e.City).HasDatabaseName("IX_Families_City");
    }
}

internal sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> entity)
    {
        entity.ToTable("Guardians");
        entity.HasKey(e => e.GuardianId);
        entity.Ignore(e => e.Id);
        entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
        entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Address).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Phone).IsRequired().HasMaxLength(20);
        entity.Property(e => e.Email).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.Latitude).HasColumnType("float");
        entity.Property(e => e.Longitude).HasColumnType("float");
    }
}

internal sealed class StudentSchoolTransferConfiguration : IEntityTypeConfiguration<StudentSchoolTransfer>
{
    public void Configure(EntityTypeBuilder<StudentSchoolTransfer> entity)
    {
        entity.ToTable("StudentSchoolTransfers");
        entity.HasKey(e => e.TransferId);
        entity.Property(e => e.PickupAddress).HasMaxLength(300);
        entity.Property(e => e.DropoffAddress).HasMaxLength(300);
        entity.Property(e => e.Notes).HasMaxLength(1000);
        entity.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.FromDestination)
            .WithMany()
            .HasForeignKey(e => e.FromDestinationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(e => e.ToDestination)
            .WithMany()
            .HasForeignKey(e => e.ToDestinationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(e => e.StudentId).HasDatabaseName("IX_StudentSchoolTransfers_Student");
        entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_StudentSchoolTransfers_Active");
    }
}
