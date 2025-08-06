using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBusDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Vehicles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SpecialNeeds",
                table: "Students",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<int>(
                name: "RouteAssignmentId",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Boundaries",
                table: "Routes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Routes",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RouteDescription",
                table: "Routes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RouteAssignments",
                columns: table => new
                {
                    RouteAssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RouteId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    AssignmentDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteAssignments", x => x.RouteAssignmentId);
                    table.ForeignKey(
                        name: "FK_RouteAssignments_Routes_RouteId",
                        column: x => x.RouteId,
                        principalTable: "Routes",
                        principalColumn: "RouteID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteAssignments_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Vehicles",
                keyColumn: "VehicleId",
                keyValue: 1,
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Vehicles",
                keyColumn: "VehicleId",
                keyValue: 2,
                column: "Description",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Students_RouteAssignmentId",
                table: "Students",
                column: "RouteAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteAssignments_RouteId",
                table: "RouteAssignments",
                column: "RouteId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteAssignments_VehicleId",
                table: "RouteAssignments",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_RouteAssignments_RouteAssignmentId",
                table: "Students",
                column: "RouteAssignmentId",
                principalTable: "RouteAssignments",
                principalColumn: "RouteAssignmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_RouteAssignments_RouteAssignmentId",
                table: "Students");

            migrationBuilder.DropTable(
                name: "RouteAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Students_RouteAssignmentId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RouteAssignmentId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Boundaries",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "RouteDescription",
                table: "Routes");

            migrationBuilder.AlterColumn<bool>(
                name: "SpecialNeeds",
                table: "Students",
                type: "bit",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "");
        }
    }
}
