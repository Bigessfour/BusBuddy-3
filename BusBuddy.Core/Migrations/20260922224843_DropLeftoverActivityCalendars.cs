using System;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <summary>
    /// TripEvent is the only trip store. Copy leftover calendar rows onto TripEvents
    /// (no RouteId), then drop Activities and ActivitySchedule.
    /// </summary>
    [DbContext(typeof(BusBuddyDbContext))]
    [Migration("20260922224843_DropLeftoverActivityCalendars")]
    public partial class DropLeftoverActivityCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            CopyLeftoverCalendars(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_StudentSchedules_ActivitySchedule",
                table: "StudentSchedules");

            migrationBuilder.DropTable(
                name: "ActivitySchedule");

            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_StudentSchedules_ActivityScheduleId",
                table: "StudentSchedules");

            migrationBuilder.DropColumn(
                name: "ActivityScheduleId",
                table: "StudentSchedules");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Make",
                table: "Vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Unknown",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "StudentDeletionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Schedules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Scheduled",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DriversLicenseType",
                table: "Drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Standard",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DriverName",
                table: "Drivers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Unknown Driver",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "");
        }

        /// <summary>
        /// Keep real leftover rows as TripEvents. Do not set RouteId. Skip a row that already
        /// matches a trip on the same date, driver, and place.
        /// </summary>
        private static void CopyLeftoverCalendars(MigrationBuilder migrationBuilder)
        {
            if (MigrationSql.IsNpgsql(migrationBuilder))
            {
                migrationBuilder.Sql(
                    """
                    INSERT INTO "TripEvents" (
                        "Type", "LeaveTime", "ReturnTime", "VehicleId", "DriverId",
                        "StudentCount", "AdultSupervisorCount", "ApprovalRequired", "IsApproved",
                        "IsMultiAsset", "IsOvernightPending", "ReturnIsNextDay",
                        "Destination", "DestinationName", "TripNotes", "Status", "SchoolYear",
                        "GroupOrActivity", "TripDate", "PickupTime", "ReturnClockTime",
                        "PlannedHeadcount", "POCName", "CustomType")
                    SELECT
                        12,
                        (("ScheduledDate" AT TIME ZONE 'UTC')::date + "ScheduledLeaveTime") AT TIME ZONE 'UTC',
                        (("ScheduledDate" AT TIME ZONE 'UTC')::date + "ScheduledEventTime") AT TIME ZONE 'UTC',
                        NULLIF("ScheduledVehicleId", 0),
                        NULLIF("ScheduledDriverId", 0),
                        COALESCE("ScheduledRiders", 0),
                        0, FALSE, FALSE, FALSE, FALSE, FALSE,
                        "ScheduledDestination",
                        "ScheduledDestination",
                        "Notes",
                        CASE WHEN "Status" ILIKE 'Cancelled' THEN 'Cancelled' ELSE 'Draft' END,
                        '',
                        LEFT("TripType", 200),
                        "ScheduledDate",
                        "ScheduledLeaveTime",
                        "ScheduledEventTime",
                        "ScheduledRiders",
                        LEFT(COALESCE("RequestedBy", ''), 100),
                        LEFT("TripType", 100)
                    FROM "ActivitySchedule" AS src
                    WHERE src."TripEventId" IS NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM "TripEvents" AS trip
                          WHERE (trip."TripDate" AT TIME ZONE 'UTC')::date = (src."ScheduledDate" AT TIME ZONE 'UTC')::date
                            AND COALESCE(trip."DriverId", 0) = COALESCE(NULLIF(src."ScheduledDriverId", 0), 0)
                            AND COALESCE(trip."DestinationName", '') = src."ScheduledDestination");

                    INSERT INTO "TripEvents" (
                        "Type", "LeaveTime", "ReturnTime", "VehicleId", "DriverId",
                        "StudentCount", "AdultSupervisorCount", "ApprovalRequired", "IsApproved",
                        "IsMultiAsset", "IsOvernightPending", "ReturnIsNextDay",
                        "Destination", "DestinationName", "TripNotes", "Status", "SchoolYear",
                        "GroupOrActivity", "TripDate", "PickupTime", "ReturnClockTime",
                        "PlannedHeadcount", "POCName", "CustomType")
                    SELECT
                        12,
                        (("Date" AT TIME ZONE 'UTC')::date + "LeaveTime") AT TIME ZONE 'UTC',
                        (("Date" AT TIME ZONE 'UTC')::date + "ReturnTime") AT TIME ZONE 'UTC',
                        NULLIF("AssignedVehicleId", 0),
                        "DriverId",
                        COALESCE("ExpectedPassengers", 0),
                        0, FALSE, FALSE, FALSE, FALSE, FALSE,
                        "Destination",
                        "Destination",
                        "Notes",
                        CASE WHEN "Status" ILIKE 'Cancelled' THEN 'Cancelled' ELSE 'Draft' END,
                        '',
                        LEFT("ActivityType", 200),
                        "Date",
                        "LeaveTime",
                        "ReturnTime",
                        "ExpectedPassengers",
                        LEFT(COALESCE("RequestedBy", ''), 100),
                        LEFT("ActivityType", 100)
                    FROM "Activities" AS src
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "TripEvents" AS trip
                        WHERE (trip."TripDate" AT TIME ZONE 'UTC')::date = (src."Date" AT TIME ZONE 'UTC')::date
                          AND COALESCE(trip."DriverId", 0) = COALESCE(src."DriverId", 0)
                          AND COALESCE(trip."DestinationName", '') = src."Destination");

                    DELETE FROM "StudentSchedules"
                    WHERE "ActivityScheduleId" IS NOT NULL AND "ScheduleId" IS NULL;
                    """);
                return;
            }

            migrationBuilder.Sql(
                """
                INSERT INTO [TripEvents] (
                    [Type], [LeaveTime], [ReturnTime], [VehicleId], [DriverId],
                    [StudentCount], [AdultSupervisorCount], [ApprovalRequired], [IsApproved],
                    [IsMultiAsset], [IsOvernightPending], [ReturnIsNextDay],
                    [Destination], [DestinationName], [TripNotes], [Status], [SchoolYear],
                    [GroupOrActivity], [TripDate], [PickupTime], [ReturnClockTime],
                    [PlannedHeadcount], [POCName], [CustomType])
                SELECT
                    12,
                    DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(src.[ScheduledLeaveTime] AS time)), CAST(CAST(src.[ScheduledDate] AS date) AS datetime2)),
                    DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(src.[ScheduledEventTime] AS time)), CAST(CAST(src.[ScheduledDate] AS date) AS datetime2)),
                    NULLIF(src.[ScheduledVehicleId], 0),
                    NULLIF(src.[ScheduledDriverId], 0),
                    COALESCE(src.[ScheduledRiders], 0),
                    0, 0, 0, 0, 0, 0,
                    src.[ScheduledDestination],
                    src.[ScheduledDestination],
                    src.[Notes],
                    CASE WHEN src.[Status] LIKE 'Cancelled' THEN 'Cancelled' ELSE 'Draft' END,
                    '',
                    LEFT(src.[TripType], 200),
                    src.[ScheduledDate],
                    src.[ScheduledLeaveTime],
                    src.[ScheduledEventTime],
                    src.[ScheduledRiders],
                    LEFT(COALESCE(src.[RequestedBy], ''), 100),
                    LEFT(src.[TripType], 100)
                FROM [ActivitySchedule] AS src
                WHERE src.[TripEventId] IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [TripEvents] AS trip
                      WHERE CAST(trip.[TripDate] AS date) = CAST(src.[ScheduledDate] AS date)
                        AND COALESCE(trip.[DriverId], 0) = COALESCE(NULLIF(src.[ScheduledDriverId], 0), 0)
                        AND COALESCE(trip.[DestinationName], '') = src.[ScheduledDestination]);

                INSERT INTO [TripEvents] (
                    [Type], [LeaveTime], [ReturnTime], [VehicleId], [DriverId],
                    [StudentCount], [AdultSupervisorCount], [ApprovalRequired], [IsApproved],
                    [IsMultiAsset], [IsOvernightPending], [ReturnIsNextDay],
                    [Destination], [DestinationName], [TripNotes], [Status], [SchoolYear],
                    [GroupOrActivity], [TripDate], [PickupTime], [ReturnClockTime],
                    [PlannedHeadcount], [POCName], [CustomType])
                SELECT
                    12,
                    DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(src.[LeaveTime] AS time)), CAST(CAST(src.[Date] AS date) AS datetime2)),
                    DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00:00' AS time), CAST(src.[ReturnTime] AS time)), CAST(CAST(src.[Date] AS date) AS datetime2)),
                    NULLIF(src.[AssignedVehicleId], 0),
                    src.[DriverId],
                    COALESCE(src.[ExpectedPassengers], 0),
                    0, 0, 0, 0, 0, 0,
                    src.[Destination],
                    src.[Destination],
                    src.[Notes],
                    CASE WHEN src.[Status] LIKE 'Cancelled' THEN 'Cancelled' ELSE 'Draft' END,
                    '',
                    LEFT(src.[ActivityType], 200),
                    src.[Date],
                    src.[LeaveTime],
                    src.[ReturnTime],
                    src.[ExpectedPassengers],
                    LEFT(COALESCE(src.[RequestedBy], ''), 100),
                    LEFT(src.[ActivityType], 100)
                FROM [Activities] AS src
                WHERE NOT EXISTS (
                    SELECT 1 FROM [TripEvents] AS trip
                    WHERE CAST(trip.[TripDate] AS date) = CAST(src.[Date] AS date)
                      AND COALESCE(trip.[DriverId], 0) = COALESCE(src.[DriverId], 0)
                      AND COALESCE(trip.[DestinationName], '') = src.[Destination]);

                DELETE FROM [StudentSchedules]
                WHERE [ActivityScheduleId] IS NOT NULL AND [ScheduleId] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "Vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Unknown");

            migrationBuilder.AlterColumn<string>(
                name: "Make",
                table: "Vehicles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "ActivityScheduleId",
                table: "StudentSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "StudentDeletionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Schedules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Scheduled");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Active");

            migrationBuilder.AlterColumn<string>(
                name: "DriversLicenseType",
                table: "Drivers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Standard");

            migrationBuilder.AlterColumn<string>(
                name: "DriverName",
                table: "Drivers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldDefaultValue: "Unknown Driver");

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    ActivityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignedVehicleId = table.Column<int>(type: "integer", nullable: false),
                    DestinationId = table.Column<int>(type: "integer", nullable: true),
                    DriverId = table.Column<int>(type: "integer", nullable: true),
                    RouteId = table.Column<int>(type: "integer", nullable: true),
                    ActivityCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ActivityName = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ActivityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    ActualCost = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Approved = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedDriverId = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DepartureTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true, defaultValue: "Activity"),
                    Destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    DestinationLatitude = table.Column<decimal>(type: "numeric(10,8)", nullable: true),
                    DestinationLongitude = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    DestinationOverride = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Directions = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DistanceMiles = table.Column<decimal>(type: "numeric(8,2)", nullable: true),
                    EstimatedArrival = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    EstimatedTravelTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EventTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ExpectedPassengers = table.Column<int>(type: "integer", nullable: true),
                    LeaveTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PickupLatitude = table.Column<decimal>(type: "numeric(10,8)", nullable: true),
                    PickupLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PickupLongitude = table.Column<decimal>(type: "numeric(11,8)", nullable: true),
                    RecurringSeriesId = table.Column<int>(type: "integer", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    ReturnTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Scheduled"),
                    StudentsCount = table.Column<int>(type: "integer", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.ActivityId);
                    table.ForeignKey(
                        name: "FK_Activities_Destinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "Destinations",
                        principalColumn: "DestinationId");
                    table.ForeignKey(
                        name: "FK_Activities_Driver",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Activities_Route",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Activities_Vehicle",
                        column: x => x.AssignedVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySchedule",
                columns: table => new
                {
                    ActivityScheduleId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduledDriverId = table.Column<int>(type: "integer", nullable: false),
                    ScheduledVehicleId = table.Column<int>(type: "integer", nullable: false),
                    TripEventId = table.Column<int>(type: "integer", nullable: true),
                    ActivityId = table.Column<int>(type: "integer", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledDestination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    ScheduledEventTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ScheduledLeaveTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ScheduledRiders = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    TripType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySchedule", x => x.ActivityScheduleId);
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "ActivityId");
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_Driver",
                        column: x => x.ScheduledDriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_TripEvents_TripEventId",
                        column: x => x.TripEventId,
                        principalTable: "TripEvents",
                        principalColumn: "TripEventId");
                    table.ForeignKey(
                        name: "FK_ActivitySchedule_Vehicle",
                        column: x => x.ScheduledVehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentSchedules_ActivityScheduleId",
                table: "StudentSchedules",
                column: "ActivityScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ActivityType",
                table: "Activities",
                column: "ActivityType");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ApprovalRequired",
                table: "Activities",
                column: "ApprovalRequired");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_BusSchedule",
                table: "Activities",
                columns: new[] { "AssignedVehicleId", "Date", "LeaveTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Date",
                table: "Activities",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DateTimeRange",
                table: "Activities",
                columns: new[] { "Date", "LeaveTime", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DestinationId",
                table: "Activities",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DriverId",
                table: "Activities",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_DriverSchedule",
                table: "Activities",
                columns: new[] { "DriverId", "Date", "LeaveTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_RouteId",
                table: "Activities",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_Status",
                table: "Activities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_VehicleId",
                table: "Activities",
                column: "AssignedVehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_ActivityId",
                table: "ActivitySchedule",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_Date",
                table: "ActivitySchedule",
                column: "ScheduledDate");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_DriverId",
                table: "ActivitySchedule",
                column: "ScheduledDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_TripEventId",
                table: "ActivitySchedule",
                column: "TripEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_TripType",
                table: "ActivitySchedule",
                column: "TripType");

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedule_VehicleId",
                table: "ActivitySchedule",
                column: "ScheduledVehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentSchedules_ActivitySchedule",
                table: "StudentSchedules",
                column: "ActivityScheduleId",
                principalTable: "ActivitySchedule",
                principalColumn: "ActivityScheduleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
