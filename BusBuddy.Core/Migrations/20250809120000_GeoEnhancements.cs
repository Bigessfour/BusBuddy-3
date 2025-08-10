using Microsoft.EntityFrameworkCore.Migrations;

namespace BusBuddy.Core.Migrations
{
    public partial class GeoEnhancements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Students geo
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

            // Drivers geo
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

            // Vehicles geo
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

            // Routes metadata
            migrationBuilder.AddColumn<string>(
                name: "WaypointsJson",
                table: "Routes",
                type: "nvarchar(4000)",
                maxLength: 4000,
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
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Latitude", table: "Students");
            migrationBuilder.DropColumn(name: "Longitude", table: "Students");
            migrationBuilder.DropColumn(name: "HomeLatitude", table: "Drivers");
            migrationBuilder.DropColumn(name: "HomeLongitude", table: "Drivers");
            migrationBuilder.DropColumn(name: "CurrentLatitude", table: "Vehicles");
            migrationBuilder.DropColumn(name: "CurrentLongitude", table: "Vehicles");
            migrationBuilder.DropColumn(name: "WaypointsJson", table: "Routes");
            migrationBuilder.DropColumn(name: "DistrictBoundaryShapefilePath", table: "Routes");
            migrationBuilder.DropColumn(name: "TownBoundaryShapefilePath", table: "Routes");
        }
    }
}
