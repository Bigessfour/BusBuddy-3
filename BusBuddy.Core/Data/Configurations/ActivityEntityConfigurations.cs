using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

internal sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> entity)
    {
        entity.ToTable("Activities");
        entity.HasKey(e => e.ActivityId);
        entity.Property(e => e.ActivityType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Destination).IsRequired().HasMaxLength(200);
        entity.Property(e => e.RequestedBy).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Scheduled");
        entity.Property(e => e.Description).HasDefaultValue("Activity");
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.Property(e => e.ActivityCategory).HasMaxLength(100);
        entity.Property(e => e.ApprovedBy).HasMaxLength(100);
        entity.Property(e => e.EstimatedCost).HasColumnType("decimal(10,2)");
        entity.Property(e => e.ActualCost).HasColumnType("decimal(10,2)");
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasOne(a => a.AssignedVehicle)
            .WithMany(v => v.Activities)
            .HasForeignKey(a => a.AssignedVehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Activities_Vehicle");
        entity.HasOne(a => a.Driver)
            .WithMany(d => d.Activities)
            .HasForeignKey(a => a.DriverId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Activities_Driver");
        entity.HasOne(a => a.Route)
            .WithMany()
            .HasForeignKey(a => a.RouteId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_Activities_Route");

        entity.HasIndex(e => e.Date).HasDatabaseName("IX_Activities_Date");
        entity.HasIndex(e => e.ActivityType).HasDatabaseName("IX_Activities_ActivityType");
        entity.HasIndex(e => e.Status).HasDatabaseName("IX_Activities_Status");
        entity.HasIndex(e => e.AssignedVehicleId).HasDatabaseName("IX_Activities_VehicleId");
        entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_Activities_DriverId");
        entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_Activities_RouteId");
        entity.HasIndex(e => new { e.Date, e.LeaveTime, e.EventTime }).HasDatabaseName("IX_Activities_DateTimeRange");
        entity.HasIndex(e => new { e.AssignedVehicleId, e.Date, e.LeaveTime }).HasDatabaseName("IX_Activities_BusSchedule");
        entity.HasIndex(e => new { e.DriverId, e.Date, e.LeaveTime }).HasDatabaseName("IX_Activities_DriverSchedule");
        entity.HasIndex(e => e.ApprovalRequired).HasDatabaseName("IX_Activities_ApprovalRequired");
    }
}

internal sealed class ActivityScheduleConfiguration : IEntityTypeConfiguration<ActivitySchedule>
{
    public void Configure(EntityTypeBuilder<ActivitySchedule> entity)
    {
        entity.ToTable("ActivitySchedule");
        entity.HasKey(e => e.ActivityScheduleId);
        entity.Property(e => e.TripType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.ScheduledDestination).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(500);

        entity.HasOne(ash => ash.ScheduledVehicle)
            .WithMany(v => v.ScheduledActivities)
            .HasForeignKey(ash => ash.ScheduledVehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ActivitySchedule_Vehicle");
        entity.HasOne(ash => ash.ScheduledDriver)
            .WithMany(d => d.ScheduledActivities)
            .HasForeignKey(ash => ash.ScheduledDriverId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ActivitySchedule_Driver");

        entity.HasIndex(e => e.ScheduledDate).HasDatabaseName("IX_ActivitySchedule_Date");
        entity.HasIndex(e => e.TripType).HasDatabaseName("IX_ActivitySchedule_TripType");
        entity.HasIndex(e => e.ScheduledVehicleId).HasDatabaseName("IX_ActivitySchedule_VehicleId");
        entity.HasIndex(e => e.ScheduledDriverId).HasDatabaseName("IX_ActivitySchedule_DriverId");
    }
}

internal sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> entity)
    {
        entity.ToTable("ActivityLogs");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Timestamp).IsRequired();
        entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
        entity.Property(e => e.User).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Details).HasMaxLength(1000);
        entity.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_ActivityLogs_Timestamp")
            .IsDescending();
    }
}

internal sealed class SchoolCalendarConfiguration : IEntityTypeConfiguration<SchoolCalendar>
{
    public void Configure(EntityTypeBuilder<SchoolCalendar> entity)
    {
        entity.ToTable("SchoolCalendar");
        entity.HasKey(e => e.CalendarId);
        entity.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.EventName).IsRequired().HasMaxLength(100);
        entity.Property(e => e.SchoolYear).IsRequired().HasMaxLength(10);
        entity.Property(e => e.Description).HasMaxLength(200);
        entity.Property(e => e.Notes).HasMaxLength(500);
        entity.HasIndex(e => e.Date).HasDatabaseName("IX_SchoolCalendar_Date");
        entity.HasIndex(e => e.EventType).HasDatabaseName("IX_SchoolCalendar_EventType");
        entity.HasIndex(e => e.SchoolYear).HasDatabaseName("IX_SchoolCalendar_SchoolYear");
        entity.HasIndex(e => e.RoutesRequired).HasDatabaseName("IX_SchoolCalendar_RoutesRequired");
    }
}

internal sealed class AIInsightConfiguration : IEntityTypeConfiguration<AIInsight>
{
    public void Configure(EntityTypeBuilder<AIInsight> entity)
    {
        entity.ToTable("AIInsights");
        entity.HasKey(e => e.InsightId);
        entity.Property(e => e.InsightType).IsRequired().HasMaxLength(50);
        entity.Property(e => e.Priority).HasMaxLength(20).HasDefaultValue("Medium");
        entity.Property(e => e.EntityReference).HasMaxLength(100);
        entity.Property(e => e.InsightDetails).HasColumnType("nvarchar(max)");
        entity.Property(e => e.Summary).IsRequired().HasMaxLength(500);
        entity.Property(e => e.RecommendedActions).HasMaxLength(1000);
        entity.Property(e => e.ConfidenceScore).HasColumnType("decimal(4,3)");
        entity.Property(e => e.Source).HasMaxLength(50).HasDefaultValue("Ollama");
        entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("New");
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.EstimatedSavings).HasColumnType("decimal(10,2)");
        entity.Property(e => e.Tags).HasMaxLength(500);

        entity.HasOne(ai => ai.Vehicle)
            .WithMany()
            .HasForeignKey(ai => ai.VehicleId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AIInsights_Vehicle");
        entity.HasOne(ai => ai.Route)
            .WithMany()
            .HasForeignKey(ai => ai.RouteId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AIInsights_Route");
        entity.HasOne(ai => ai.Driver)
            .WithMany()
            .HasForeignKey(ai => ai.DriverId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AIInsights_Driver");

        entity.HasIndex(e => e.InsightType).HasDatabaseName("IX_AIInsights_Type");
        entity.HasIndex(e => e.Priority).HasDatabaseName("IX_AIInsights_Priority");
        entity.HasIndex(e => e.Status).HasDatabaseName("IX_AIInsights_Status");
        entity.HasIndex(e => e.CreatedDate).HasDatabaseName("IX_AIInsights_CreatedDate");
        entity.HasIndex(e => e.ConfidenceScore).HasDatabaseName("IX_AIInsights_ConfidenceScore");
        entity.HasIndex(e => new { e.InsightType, e.Status }).HasDatabaseName("IX_AIInsights_TypeStatus");
        entity.HasIndex(e => new { e.VehicleId, e.InsightType }).HasDatabaseName("IX_AIInsights_VehicleType");
        entity.HasIndex(e => new { e.RouteId, e.InsightType }).HasDatabaseName("IX_AIInsights_RouteType");
        entity.HasIndex(e => new { e.DriverId, e.InsightType }).HasDatabaseName("IX_AIInsights_DriverType");
        entity.HasIndex(e => e.ExpiryDate).HasDatabaseName("IX_AIInsights_ExpiryDate");
    }
}
