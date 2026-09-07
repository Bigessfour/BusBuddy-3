using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Office trip-board columns on TripEvent. Route != Trip; no IsTrip on Route.
/// MissingInfo is allowed without a destination.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260907140000_TripEventBoardColumns")]
public partial class TripEventBoardColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var str8 = MigrationSql.StringType(migrationBuilder, 8);
        var str16 = MigrationSql.StringType(migrationBuilder, 16);
        var str32 = MigrationSql.StringType(migrationBuilder, 32);
        var str40 = MigrationSql.StringType(migrationBuilder, 40);
        var str200 = MigrationSql.StringType(migrationBuilder, 200);
        var dt = MigrationSql.DateTimeType(migrationBuilder);
        var boolType = MigrationSql.BoolType(migrationBuilder);
        var decimalType = "decimal(8,2)";

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "TripEvents",
            type: str32,
            maxLength: 32,
            nullable: false,
            defaultValue: "Draft",
            oldClrType: typeof(string),
            oldType: MigrationSql.StringType(migrationBuilder, 20),
            oldMaxLength: 20,
            oldDefaultValue: "Scheduled");

        migrationBuilder.AlterColumn<string>(
            name: "POCName",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 100),
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: MigrationSql.StringType(migrationBuilder, 100),
            oldMaxLength: 100);

        migrationBuilder.AddColumn<string>(
            name: "ExternalTicketNo",
            table: "TripEvents",
            type: str32,
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SchoolYear",
            table: "TripEvents",
            type: str16,
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "RequestingSchool",
            table: "TripEvents",
            type: str8,
            maxLength: 8,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GroupOrActivity",
            table: "TripEvents",
            type: str200,
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "TripDate",
            table: "TripEvents",
            type: dt,
            nullable: false,
            defaultValue: new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));

        migrationBuilder.AddColumn<TimeSpan>(
            name: "PickupTime",
            table: "TripEvents",
            type: "time",
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "ReturnClockTime",
            table: "TripEvents",
            type: "time",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "ReturnIsNextDay",
            table: "TripEvents",
            type: boolType,
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "OriginName",
            table: "TripEvents",
            type: str200,
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OriginLocationId",
            table: "TripEvents",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DestinationName",
            table: "TripEvents",
            type: str200,
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DestinationLocationId",
            table: "TripEvents",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PlannedHeadcount",
            table: "TripEvents",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ActualHeadcount",
            table: "TripEvents",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PlannedMiles",
            table: "TripEvents",
            type: decimalType,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PathMiles",
            table: "TripEvents",
            type: decimalType,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "LinkedTripId",
            table: "TripEvents",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsMultiAsset",
            table: "TripEvents",
            type: boolType,
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsOvernightPending",
            table: "TripEvents",
            type: boolType,
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "AssignedBusNumber",
            table: "TripEvents",
            type: str40,
            maxLength: 40,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TripEvents_TripDate",
            table: "TripEvents",
            column: "TripDate");

        migrationBuilder.CreateIndex(
            name: "IX_TripEvents_ExternalTicketNo",
            table: "TripEvents",
            column: "ExternalTicketNo",
            unique: true,
            filter: MigrationSql.NotNullFilter(migrationBuilder, "ExternalTicketNo"));

        migrationBuilder.CreateIndex(
            name: "IX_TripEvents_OriginLocationId",
            table: "TripEvents",
            column: "OriginLocationId");

        migrationBuilder.CreateIndex(
            name: "IX_TripEvents_DestinationLocationId",
            table: "TripEvents",
            column: "DestinationLocationId");

        migrationBuilder.CreateIndex(
            name: "IX_TripEvents_LinkedTripId",
            table: "TripEvents",
            column: "LinkedTripId");

        migrationBuilder.AddForeignKey(
            name: "FK_TripEvents_OriginLocation",
            table: "TripEvents",
            column: "OriginLocationId",
            principalTable: "Destinations",
            principalColumn: "DestinationId",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_TripEvents_DestinationLocation",
            table: "TripEvents",
            column: "DestinationLocationId",
            principalTable: "Destinations",
            principalColumn: "DestinationId",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_TripEvents_LinkedTrip",
            table: "TripEvents",
            column: "LinkedTripId",
            principalTable: "TripEvents",
            principalColumn: "TripEventId",
            onDelete: ReferentialAction.SetNull);

        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql(
                """UPDATE "TripEvents" SET "TripDate" = (("LeaveTime" AT TIME ZONE 'UTC')::date) WHERE "TripDate" < DATE '0002-01-01';""");
            migrationBuilder.Sql(
                """UPDATE "TripEvents" SET "PickupTime" = (("LeaveTime" AT TIME ZONE 'UTC')::time) WHERE "PickupTime" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "TripEvents" SET "DestinationName" = "Destination" WHERE COALESCE("DestinationName", '') = '' AND COALESCE("Destination", '') <> '';""");
            migrationBuilder.Sql(
                """UPDATE "TripEvents" SET "Status" = 'Draft' WHERE "Status" = 'Scheduled';""");
        }
        else
        {
            migrationBuilder.Sql(
                "UPDATE TripEvents SET TripDate = CAST(LeaveTime AS date) WHERE TripDate < '00020101';");
            migrationBuilder.Sql(
                "UPDATE TripEvents SET PickupTime = CAST(LeaveTime AS time) WHERE PickupTime IS NULL;");
            migrationBuilder.Sql(
                "UPDATE TripEvents SET DestinationName = Destination WHERE ISNULL(DestinationName, '') = '' AND ISNULL(Destination, '') <> '';");
            migrationBuilder.Sql(
                "UPDATE TripEvents SET Status = 'Draft' WHERE Status = 'Scheduled';");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_TripEvents_OriginLocation", table: "TripEvents");
        migrationBuilder.DropForeignKey(name: "FK_TripEvents_DestinationLocation", table: "TripEvents");
        migrationBuilder.DropForeignKey(name: "FK_TripEvents_LinkedTrip", table: "TripEvents");
        migrationBuilder.DropIndex(name: "IX_TripEvents_TripDate", table: "TripEvents");
        migrationBuilder.DropIndex(name: "IX_TripEvents_ExternalTicketNo", table: "TripEvents");
        migrationBuilder.DropIndex(name: "IX_TripEvents_OriginLocationId", table: "TripEvents");
        migrationBuilder.DropIndex(name: "IX_TripEvents_DestinationLocationId", table: "TripEvents");
        migrationBuilder.DropIndex(name: "IX_TripEvents_LinkedTripId", table: "TripEvents");
        migrationBuilder.DropColumn(name: "ExternalTicketNo", table: "TripEvents");
        migrationBuilder.DropColumn(name: "SchoolYear", table: "TripEvents");
        migrationBuilder.DropColumn(name: "RequestingSchool", table: "TripEvents");
        migrationBuilder.DropColumn(name: "GroupOrActivity", table: "TripEvents");
        migrationBuilder.DropColumn(name: "TripDate", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PickupTime", table: "TripEvents");
        migrationBuilder.DropColumn(name: "ReturnClockTime", table: "TripEvents");
        migrationBuilder.DropColumn(name: "ReturnIsNextDay", table: "TripEvents");
        migrationBuilder.DropColumn(name: "OriginName", table: "TripEvents");
        migrationBuilder.DropColumn(name: "OriginLocationId", table: "TripEvents");
        migrationBuilder.DropColumn(name: "DestinationName", table: "TripEvents");
        migrationBuilder.DropColumn(name: "DestinationLocationId", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PlannedHeadcount", table: "TripEvents");
        migrationBuilder.DropColumn(name: "ActualHeadcount", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PlannedMiles", table: "TripEvents");
        migrationBuilder.DropColumn(name: "PathMiles", table: "TripEvents");
        migrationBuilder.DropColumn(name: "LinkedTripId", table: "TripEvents");
        migrationBuilder.DropColumn(name: "IsMultiAsset", table: "TripEvents");
        migrationBuilder.DropColumn(name: "IsOvernightPending", table: "TripEvents");
        migrationBuilder.DropColumn(name: "AssignedBusNumber", table: "TripEvents");

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 20),
            maxLength: 20,
            nullable: false,
            defaultValue: "Scheduled",
            oldClrType: typeof(string),
            oldType: MigrationSql.StringType(migrationBuilder, 32),
            oldMaxLength: 32,
            oldDefaultValue: "Draft");

        migrationBuilder.AlterColumn<string>(
            name: "POCName",
            table: "TripEvents",
            type: MigrationSql.StringType(migrationBuilder, 100),
            maxLength: 100,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: MigrationSql.StringType(migrationBuilder, 100),
            oldMaxLength: 100,
            oldNullable: true);
    }
}
