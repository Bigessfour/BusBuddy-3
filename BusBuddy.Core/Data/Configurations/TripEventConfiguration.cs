using BusBuddy.Core.Models.Trips;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BusBuddy.Core.Data.Configurations;

/// <summary>One-date trip board. Route != Trip — never clone Route onto this table.</summary>
internal sealed class TripEventConfiguration : IEntityTypeConfiguration<TripEvent>
{
    private readonly string? _providerName;

    public TripEventConfiguration(string? providerName) => _providerName = providerName;

    public void Configure(EntityTypeBuilder<TripEvent> entity)
    {
        entity.ToTable("TripEvents");
        entity.HasKey(e => e.TripEventId);
        entity.Property(e => e.Type).IsRequired();
        entity.Property(e => e.CustomType).HasMaxLength(100);
        entity.Property(e => e.POCName).IsRequired(false).HasMaxLength(100);
        entity.Property(e => e.POCPhone).HasMaxLength(20);
        entity.Property(e => e.POCEmail).HasMaxLength(100);
        entity.Property(e => e.Destination).HasMaxLength(200);
        entity.Property(e => e.DestinationName).HasMaxLength(200);
        entity.Property(e => e.OriginName).HasMaxLength(200);
        entity.Property(e => e.SpecialRequirements).HasMaxLength(500);
        entity.Property(e => e.TripNotes).HasMaxLength(1000);
        entity.Property(e => e.Status).HasMaxLength(32).HasDefaultValue(TripStatus.Draft);
        entity.Property(e => e.ExternalTicketNo).HasMaxLength(32);
        entity.Property(e => e.SchoolYear).HasMaxLength(16);
        entity.Property(e => e.RequestingSchool).HasMaxLength(8);
        entity.Property(e => e.GroupOrActivity).HasMaxLength(200);
        entity.Property(e => e.AssignedBusNumber).HasMaxLength(40);
        entity.Property(e => e.PlannedMiles).HasColumnType("decimal(8,2)");
        entity.Property(e => e.PathMiles).HasColumnType("decimal(8,2)");
        entity.Property(e => e.ApprovedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedBy).HasMaxLength(100);
        entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasOne(te => te.Vehicle)
            .WithMany()
            .HasForeignKey(te => te.VehicleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TripEvents_Vehicle");
        entity.HasOne(te => te.Driver)
            .WithMany()
            .HasForeignKey(te => te.DriverId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TripEvents_Driver");
        entity.HasOne(te => te.Route)
            .WithMany()
            .HasForeignKey(te => te.RouteId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TripEvents_Route");
        entity.HasOne(te => te.OriginLocation)
            .WithMany()
            .HasForeignKey(te => te.OriginLocationId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TripEvents_OriginLocation");
        entity.HasOne(te => te.DestinationLocation)
            .WithMany()
            .HasForeignKey(te => te.DestinationLocationId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TripEvents_DestinationLocation");
        entity.HasOne(te => te.LinkedTrip)
            .WithMany()
            .HasForeignKey(te => te.LinkedTripId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_TripEvents_LinkedTrip");

        entity.HasIndex(e => e.LeaveTime).HasDatabaseName("IX_TripEvents_LeaveTime");
        entity.HasIndex(e => e.Type).HasDatabaseName("IX_TripEvents_Type");
        entity.HasIndex(e => e.Status).HasDatabaseName("IX_TripEvents_Status");
        entity.HasIndex(e => e.VehicleId).HasDatabaseName("IX_TripEvents_VehicleId");
        entity.HasIndex(e => e.DriverId).HasDatabaseName("IX_TripEvents_DriverId");
        entity.HasIndex(e => e.RouteId).HasDatabaseName("IX_TripEvents_RouteId");
        entity.HasIndex(e => e.TripDate).HasDatabaseName("IX_TripEvents_TripDate");

        var ticketIndex = entity.HasIndex(e => e.ExternalTicketNo)
            .IsUnique()
            .HasDatabaseName("IX_TripEvents_ExternalTicketNo");
        ticketIndex.HasFilter(
            string.Equals(_providerName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal)
                ? "\"ExternalTicketNo\" IS NOT NULL"
                : "[ExternalTicketNo] IS NOT NULL");

        entity.HasIndex(e => new { e.VehicleId, e.LeaveTime }).HasDatabaseName("IX_TripEvents_BusSchedule");
        entity.HasIndex(e => new { e.DriverId, e.LeaveTime }).HasDatabaseName("IX_TripEvents_DriverSchedule");
        entity.HasIndex(e => e.ApprovalRequired).HasDatabaseName("IX_TripEvents_ApprovalRequired");
    }
}
