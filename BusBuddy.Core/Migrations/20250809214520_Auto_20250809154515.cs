using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20250809154515 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLatitude",
                table: "Vehicles",
                type: "decimal(10,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentLongitude",
                table: "Vehicles",
                type: "decimal(11,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Students",
                type: "decimal(10,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Students",
                type: "decimal(11,8)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DistrictBoundaryShapefilePath",
                table: "Routes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TownBoundaryShapefilePath",
                table: "Routes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WaypointsJson",
                table: "Routes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HomeLatitude",
                table: "Drivers",
                type: "decimal(10,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HomeLongitude",
                table: "Drivers",
                type: "decimal(11,8)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Drivers",
                keyColumn: "DriverID",
                keyValue: 1,
                columns: new[] { "HomeLatitude", "HomeLongitude" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Drivers",
                keyColumn: "DriverID",
                keyValue: 2,
                columns: new[] { "HomeLatitude", "HomeLongitude" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Vehicles",
                keyColumn: "VehicleId",
                keyValue: 1,
                columns: new[] { "CurrentLatitude", "CurrentLongitude" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Vehicles",
                keyColumn: "VehicleId",
                keyValue: 2,
                columns: new[] { "CurrentLatitude", "CurrentLongitude" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CurrentLocation",
                table: "Vehicles",
                columns: new[] { "CurrentLatitude", "CurrentLongitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_HomeLocation",
                table: "Drivers",
                columns: new[] { "HomeLatitude", "HomeLongitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_CurrentLocation",
                table: "Vehicles");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_HomeLocation",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "CurrentLatitude",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CurrentLongitude",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "DistrictBoundaryShapefilePath",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "TownBoundaryShapefilePath",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "WaypointsJson",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "HomeLatitude",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "HomeLongitude",
                table: "Drivers");
        }
    }
}
