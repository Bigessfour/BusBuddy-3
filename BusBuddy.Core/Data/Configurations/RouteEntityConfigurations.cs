using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

/// <summary>Daily published runs. Route != Trip — do not add IsTrip here.</summary>
internal sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> entity)
    {
        entity.ToTable("Routes");
        entity.HasKey(e => e.RouteId);
        entity.Property(e => e.RouteId).HasColumnName("RouteID");
        entity.Property(e => e.RouteName).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.IsSpecialNeedsRoute).HasDefaultValue(false);
        entity.Property(e => e.Session).IsRequired().HasMaxLength(20).HasDefaultValue(RouteSession.AM);
        entity.Property(e => e.AMVehicleId).HasColumnName("AMVehicleID");
        entity.Property(e => e.AMDriverId).HasColumnName("AMDriverID");
        entity.Property(e => e.PMVehicleId).HasColumnName("PMVehicleID");
        entity.Property(e => e.PMDriverId).HasColumnName("PMDriverID");
        entity.Property(e => e.AMBeginMiles).HasColumnType("decimal(10,2)");
        entity.Property(e => e.AMEndMiles).HasColumnType("decimal(10,2)");
        entity.Property(e => e.PMBeginMiles).HasColumnType("decimal(10,2)");
        entity.Property(e => e.PMEndMiles).HasColumnType("decimal(10,2)");
        entity.Property(e => e.WaypointsJson);

        entity.HasOne(r => r.AMVehicle)
            .WithMany(v => v.AMRoutes)
            .HasForeignKey(r => r.AMVehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Routes_AMVehicle");
        entity.HasOne(r => r.AMDriver)
            .WithMany(d => d.AMRoutes)
            .HasForeignKey(r => r.AMDriverId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Routes_AMDriver");
        entity.HasOne(r => r.PMVehicle)
            .WithMany(v => v.PMRoutes)
            .HasForeignKey(r => r.PMVehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Routes_PMVehicle");
        entity.HasOne(r => r.PMDriver)
            .WithMany(d => d.PMRoutes)
            .HasForeignKey(r => r.PMDriverId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Routes_PMDriver");

        entity.HasIndex(e => e.Session).HasDatabaseName("IX_Routes_Session");
        entity.HasIndex(e => e.Date).HasDatabaseName("IX_Routes_Date");
        entity.HasIndex(e => e.RouteName).HasDatabaseName("IX_Routes_RouteName");
        entity.HasIndex(e => new { e.Date, e.RouteName }).IsUnique().HasDatabaseName("IX_Routes_DateRouteName");
        entity.HasIndex(e => e.AMVehicleId).HasDatabaseName("IX_Routes_AMVehicleId");
        entity.HasIndex(e => e.PMVehicleId).HasDatabaseName("IX_Routes_PMVehicleId");
        entity.HasIndex(e => e.AMDriverId).HasDatabaseName("IX_Routes_AMDriverId");
        entity.HasIndex(e => e.PMDriverId).HasDatabaseName("IX_Routes_PMDriverId");
    }
}

internal sealed class RouteAssignmentConfiguration : IEntityTypeConfiguration<RouteAssignment>
{
    public void Configure(EntityTypeBuilder<RouteAssignment> entity)
    {
        entity.ToTable("RouteAssignments");
        entity.HasKey(e => e.RouteAssignmentId);
        entity.HasOne(e => e.Route)
            .WithMany()
            .HasForeignKey(e => e.RouteId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        entity.HasOne(e => e.Vehicle)
            .WithMany()
            .HasForeignKey(e => e.VehicleId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
        entity.HasOne(e => e.Guardian)
            .WithMany()
            .HasForeignKey(e => e.GuardianId);
        entity.HasIndex(e => e.RouteId);
        entity.HasIndex(e => e.VehicleId);
        entity.HasIndex(e => e.GuardianId);
    }
}

internal sealed class RouteStopConfiguration : IEntityTypeConfiguration<RouteStop>
{
    public void Configure(EntityTypeBuilder<RouteStop> entity)
    {
        entity.ToTable("RouteStops");
        entity.HasKey(e => e.RouteStopId);
        entity.Property(e => e.StopName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.StopAddress).HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasOne(rs => rs.Route)
            .WithMany()
            .HasForeignKey(rs => rs.RouteId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RouteStops_Route");
        entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_RouteStops_RouteId");
        entity.HasIndex(e => new { e.RouteId, e.StopOrder }).HasDatabaseName("IX_RouteStops_RouteOrder");
    }
}

internal sealed class RouteRiderExceptionConfiguration : IEntityTypeConfiguration<RouteRiderException>
{
    public void Configure(EntityTypeBuilder<RouteRiderException> entity)
    {
        entity.ToTable("RouteRiderExceptions");
        entity.HasKey(e => e.RouteRiderExceptionId);
        entity.Property(e => e.Reason).HasMaxLength(200);
        entity.HasOne(e => e.Route)
            .WithMany()
            .HasForeignKey(e => e.RouteId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_RouteRiderExceptions_Route");
        entity.HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_RouteRiderExceptions_Student");
        entity.HasIndex(e => new { e.RouteId, e.StudentId, e.ExceptionDate })
            .IsUnique()
            .HasDatabaseName("IX_RouteRiderExceptions_RouteStudentDate");
    }
}

internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> entity)
    {
        entity.ToTable("Schedules");
        entity.HasKey(e => e.ScheduleId);
        entity.Property(e => e.BusId).HasColumnName("VehicleId");
        entity.Property(e => e.Status).HasDefaultValue("Scheduled");
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasOne(s => s.Bus)
            .WithMany(b => b.Schedules)
            .HasForeignKey(s => s.BusId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Schedules_Bus");
        entity.HasOne(s => s.Route)
            .WithMany(r => r.Schedules)
            .HasForeignKey(s => s.RouteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Schedules_Route");
        entity.HasOne(s => s.Driver)
            .WithMany(d => d.Schedules)
            .HasForeignKey(s => s.DriverId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Schedules_Driver");

        entity.HasIndex(e => new { e.RouteId, e.BusId, e.DepartureTime }).IsUnique().HasDatabaseName("IX_Schedules_RouteBusDeparture");
        entity.HasIndex(e => e.ScheduleDate).HasDatabaseName("IX_Schedules_Date");
        entity.HasIndex(e => e.BusId).HasDatabaseName("IX_Schedules_BusId");
        entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_Schedules_DriverId");
        entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_Schedules_RouteId");
    }
}

internal sealed class StudentScheduleConfiguration : IEntityTypeConfiguration<StudentSchedule>
{
    public void Configure(EntityTypeBuilder<StudentSchedule> entity)
    {
        entity.ToTable("StudentSchedules");
        entity.HasKey(e => e.StudentScheduleId);
        entity.Property(e => e.AssignmentType).IsRequired().HasMaxLength(20);
        entity.Property(e => e.PickupLocation).HasMaxLength(100);
        entity.Property(e => e.DropoffLocation).HasMaxLength(100);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);

        entity.HasOne(ss => ss.Student)
            .WithMany(s => s.StudentSchedules)
            .HasForeignKey(ss => ss.StudentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StudentSchedules_Student");
        entity.HasOne(ss => ss.Schedule)
            .WithMany(s => s.StudentSchedules)
            .HasForeignKey(ss => ss.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StudentSchedules_Schedule");
        entity.HasOne(ss => ss.ActivitySchedule)
            .WithMany(a => a.StudentSchedules)
            .HasForeignKey(ss => ss.ActivityScheduleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_StudentSchedules_ActivitySchedule");

        entity.HasIndex(e => e.StudentId).HasDatabaseName("IX_StudentSchedules_StudentId");
        entity.HasIndex(e => e.ScheduleId).HasDatabaseName("IX_StudentSchedules_ScheduleId");
        entity.HasIndex(e => e.ActivityScheduleId).HasDatabaseName("IX_StudentSchedules_ActivityScheduleId");
        entity.HasIndex(e => e.AssignmentType).HasDatabaseName("IX_StudentSchedules_AssignmentType");
        entity.HasIndex(e => new { e.StudentId, e.ScheduleId }).IsUnique().HasDatabaseName("IX_StudentSchedules_StudentSchedule");
    }
}
