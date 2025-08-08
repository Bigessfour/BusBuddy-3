using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RouteAssignments_Buses_VehicleId",
                table: "RouteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SportsEvents_Buses_VehicleId",
                table: "SportsEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Buses",
                table: "Buses");

            migrationBuilder.RenameTable(
                name: "Buses",
                newName: "Vehicles");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_VINNumber",
                table: "Vehicles",
                newName: "IX_Vehicles_VINNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_Status",
                table: "Vehicles",
                newName: "IX_Vehicles_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_MakeModelYear",
                table: "Vehicles",
                newName: "IX_Vehicles_MakeModelYear");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_LicenseNumber",
                table: "Vehicles",
                newName: "IX_Vehicles_LicenseNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_InsuranceExpiryDate",
                table: "Vehicles",
                newName: "IX_Vehicles_InsuranceExpiryDate");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_FleetType",
                table: "Vehicles",
                newName: "IX_Vehicles_FleetType");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_DateLastInspection",
                table: "Vehicles",
                newName: "IX_Vehicles_DateLastInspection");

            migrationBuilder.RenameIndex(
                name: "IX_Buses_BusNumber",
                table: "Vehicles",
                newName: "IX_Vehicles_BusNumber");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RouteAssignments_Vehicles_VehicleId",
                table: "RouteAssignments",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SportsEvents_Vehicles_VehicleId",
                table: "SportsEvents",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RouteAssignments_Vehicles_VehicleId",
                table: "RouteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SportsEvents_Vehicles_VehicleId",
                table: "SportsEvents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles");

            migrationBuilder.RenameTable(
                name: "Vehicles",
                newName: "Buses");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_VINNumber",
                table: "Buses",
                newName: "IX_Buses_VINNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_Status",
                table: "Buses",
                newName: "IX_Buses_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_MakeModelYear",
                table: "Buses",
                newName: "IX_Buses_MakeModelYear");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_LicenseNumber",
                table: "Buses",
                newName: "IX_Buses_LicenseNumber");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_InsuranceExpiryDate",
                table: "Buses",
                newName: "IX_Buses_InsuranceExpiryDate");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_FleetType",
                table: "Buses",
                newName: "IX_Buses_FleetType");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_DateLastInspection",
                table: "Buses",
                newName: "IX_Buses_DateLastInspection");

            migrationBuilder.RenameIndex(
                name: "IX_Vehicles_BusNumber",
                table: "Buses",
                newName: "IX_Buses_BusNumber");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Buses",
                table: "Buses",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RouteAssignments_Buses_VehicleId",
                table: "RouteAssignments",
                column: "VehicleId",
                principalTable: "Buses",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SportsEvents_Buses_VehicleId",
                table: "SportsEvents",
                column: "VehicleId",
                principalTable: "Buses",
                principalColumn: "VehicleId");
        }
    }
}
