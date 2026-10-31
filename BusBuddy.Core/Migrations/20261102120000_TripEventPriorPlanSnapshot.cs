using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Confirmed → Changed keeps the prior place, times, bus, and driver on TripEvent (#117).
/// Live board columns stay the new plan; this is a small prior-value snapshot, not a history table.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20261102120000_TripEventPriorPlanSnapshot")]
public partial class TripEventPriorPlanSnapshot : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var dt = MigrationSql.DateTimeType(migrationBuilder);
        var boolType = MigrationSql.BoolType(migrationBuilder);
        var timeType = MigrationSql.TimeSpanType(migrationBuilder);
        var intType = MigrationSql.IntType(migrationBuilder);

        migrationBuilder.AddColumn<DateTime>(
            name: "PriorPlanCapturedUtc",
            table: "TripEvents",
            type: dt,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PriorDestinationName",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 200),
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PriorOriginName",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 200),
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "PriorPickupTime",
            table: "TripEvents",
            type: timeType,
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "PriorReturnClockTime",
            table: "TripEvents",
            type: timeType,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "PriorReturnIsNextDay",
            table: "TripEvents",
            type: boolType,
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "PriorVehicleId",
            table: "TripEvents",
            type: intType,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PriorAssignedBusNumber",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 40),
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PriorDriverId",
            table: "TripEvents",
            type: intType,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PriorDriverName",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 100),
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PriorPlanCapturedUtc", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorDestinationName", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorOriginName", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorPickupTime", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorReturnClockTime", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorReturnIsNextDay", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorVehicleId", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorAssignedBusNumber", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorDriverId", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PriorDriverName", table: "TripEvents");
    }
}
