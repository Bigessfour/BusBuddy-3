using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShapefileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
    }
}
