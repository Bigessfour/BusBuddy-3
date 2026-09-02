using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// District pickup stop catalog + Student.PickupStopId for shared block stops.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260901193000_PickupStops")]
public partial class PickupStops : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PickupStops",
            columns: table => new
            {
                PickupStopId = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: false),
                Address = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 300), maxLength: 300, nullable: true),
                Latitude = table.Column<decimal>(type: "decimal(10,8)", nullable: false),
                Longitude = table.Column<decimal>(type: "decimal(11,8)", nullable: false),
                StopType = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 20), maxLength: 20, nullable: false),
                Active = table.Column<bool>(type: MigrationSql.BoolType(migrationBuilder), nullable: false, defaultValue: true),
                Notes = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 500), maxLength: 500, nullable: true),
                CreatedDate = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                CreatedBy = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PickupStops", x => x.PickupStopId);
            });

        migrationBuilder.AddColumn<int>(
            name: "PickupStopId",
            table: "Students",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Students_PickupStop",
            table: "Students",
            column: "PickupStopId");

        migrationBuilder.CreateIndex(
            name: "IX_PickupStops_Active",
            table: "PickupStops",
            column: "Active");

        migrationBuilder.AddForeignKey(
            name: "FK_Students_PickupStops_PickupStopId",
            table: "Students",
            column: "PickupStopId",
            principalTable: "PickupStops",
            principalColumn: "PickupStopId",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Students_PickupStops_PickupStopId",
            table: "Students");

        migrationBuilder.DropIndex(
            name: "IX_Students_PickupStop",
            table: "Students");

        migrationBuilder.DropColumn(
            name: "PickupStopId",
            table: "Students");

        migrationBuilder.DropTable(
            name: "PickupStops");
    }
}
