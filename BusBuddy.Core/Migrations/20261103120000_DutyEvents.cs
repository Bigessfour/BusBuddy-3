using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Shared driver+bus duty log (#112). Defect rows may point Maintenance.SourceDutyEventId here.
/// Fuel EventType is attribution only — gallons stay on Fuel.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20261103120000_DutyEvents")]
public partial class DutyEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DutyEvents",
            columns: table => new
            {
                DutyEventId = table.Column<int>(type: MigrationSql.IntType(migrationBuilder), nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DriverId = table.Column<int>(type: MigrationSql.IntType(migrationBuilder), nullable: false),
                BusId = table.Column<int>(type: MigrationSql.IntType(migrationBuilder), nullable: false),
                Session = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 8), maxLength: 8, nullable: false),
                EventType = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 16), maxLength: 16, nullable: false),
                EventedAt = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                Notes = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 1000), maxLength: 1000, nullable: true),
                FuelId = table.Column<int>(type: MigrationSql.IntType(migrationBuilder), nullable: true),
                OdometerReading = table.Column<int>(type: MigrationSql.IntType(migrationBuilder), nullable: true),
                CreatedDate = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false,
                    defaultValueSql: MigrationSql.UtcNow(migrationBuilder))
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DutyEvents", x => x.DutyEventId);
                table.ForeignKey(
                    name: "FK_DutyEvents_Driver",
                    column: x => x.DriverId,
                    principalTable: "Drivers",
                    principalColumn: "DriverId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DutyEvents_Bus",
                    column: x => x.BusId,
                    principalTable: "Vehicles",
                    principalColumn: "VehicleId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_DutyEvents_Fuel",
                    column: x => x.FuelId,
                    principalTable: "Fuel",
                    principalColumn: "FuelId",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DutyEvents_EventedAt",
            table: "DutyEvents",
            column: "EventedAt");

        migrationBuilder.CreateIndex(
            name: "IX_DutyEvents_DriverId",
            table: "DutyEvents",
            column: "DriverId");

        migrationBuilder.CreateIndex(
            name: "IX_DutyEvents_BusId",
            table: "DutyEvents",
            column: "BusId");

        migrationBuilder.CreateIndex(
            name: "IX_DutyEvents_BusDriverDay",
            table: "DutyEvents",
            columns: new[] { "BusId", "DriverId", "EventedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_DutyEvents_TypeSessionDay",
            table: "DutyEvents",
            columns: new[] { "EventType", "Session", "EventedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_DutyEvents_FuelId",
            table: "DutyEvents",
            column: "FuelId");

        migrationBuilder.AddColumn<int>(
            name: "SourceDutyEventId",
            table: "Maintenance",
            type: MigrationSql.IntType(migrationBuilder),
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Maintenance_SourceDutyEventId",
            table: "Maintenance",
            column: "SourceDutyEventId");

        migrationBuilder.AddForeignKey(
            name: "FK_Maintenance_SourceDutyEvent",
            table: "Maintenance",
            column: "SourceDutyEventId",
            principalTable: "DutyEvents",
            principalColumn: "DutyEventId",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Maintenance_SourceDutyEvent",
            table: "Maintenance");

        migrationBuilder.DropIndex(
            name: "IX_Maintenance_SourceDutyEventId",
            table: "Maintenance");

        migrationBuilder.DropColumn(
            name: "SourceDutyEventId",
            table: "Maintenance");

        migrationBuilder.DropTable(name: "DutyEvents");
    }
}
