using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BusBuddyDbContext))]
    [Migration("20260916200000_DropRouteShapefilePaths")]
    public partial class DropRouteShapefilePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 20250814210725_RemoveShapefileColumns was empty, so these leftover columns
            // are still on Routes. Maps use Google tiles only (specs/maps.md).
            migrationBuilder.DropColumn(
                name: "DistrictBoundaryShapefilePath",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "TownBoundaryShapefilePath",
                table: "Routes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistrictBoundaryShapefilePath",
                table: "Routes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TownBoundaryShapefilePath",
                table: "Routes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
