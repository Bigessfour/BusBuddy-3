using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialEntra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RouteAssignments_Vehicles_VehicleId",
                table: "RouteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SportsEvents_Vehicles_VehicleId",
                table: "SportsEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Family",
                table: "Students");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Vehicles",
                table: "Vehicles");

            migrationBuilder.RenameTable(
                name: "Vehicles",
                newName: "Buses");

            migrationBuilder.RenameIndex(
                name: "IX_TripEvents_VehicleSchedule",
                table: "TripEvents",
                newName: "IX_TripEvents_BusSchedule");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_VehicleSchedule",
                table: "Activities",
                newName: "IX_Activities_BusSchedule");

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

            migrationBuilder.AlterColumn<int>(
                name: "FamilyId",
                table: "Students",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "School",
                table: "Routes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldDefaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GuardianId",
                table: "RouteAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Buses",
                table: "Buses",
                column: "VehicleId");

            migrationBuilder.CreateTable(
                name: "Guardians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GuardianId = table.Column<int>(type: "int", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: ""),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FamilyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guardians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guardians_Family",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "FamilyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteAssignments_GuardianId",
                table: "RouteAssignments",
                column: "GuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_Guardians_FamilyId",
                table: "Guardians",
                column: "FamilyId");

            migrationBuilder.AddForeignKey(
                name: "FK_RouteAssignments_Buses_VehicleId",
                table: "RouteAssignments",
                column: "VehicleId",
                principalTable: "Buses",
                principalColumn: "VehicleId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RouteAssignments_Guardians_GuardianId",
                table: "RouteAssignments",
                column: "GuardianId",
                principalTable: "Guardians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SportsEvents_Buses_VehicleId",
                table: "SportsEvents",
                column: "VehicleId",
                principalTable: "Buses",
                principalColumn: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Family",
                table: "Students",
                column: "FamilyId",
                principalTable: "Families",
                principalColumn: "FamilyId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RouteAssignments_Buses_VehicleId",
                table: "RouteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_RouteAssignments_Guardians_GuardianId",
                table: "RouteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_SportsEvents_Buses_VehicleId",
                table: "SportsEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Family",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Guardians");

            migrationBuilder.DropIndex(
                name: "IX_RouteAssignments_GuardianId",
                table: "RouteAssignments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Buses",
                table: "Buses");

            migrationBuilder.DropColumn(
                name: "GuardianId",
                table: "RouteAssignments");

            migrationBuilder.RenameTable(
                name: "Buses",
                newName: "Vehicles");

            migrationBuilder.RenameIndex(
                name: "IX_TripEvents_BusSchedule",
                table: "TripEvents",
                newName: "IX_TripEvents_VehicleSchedule");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_BusSchedule",
                table: "Activities",
                newName: "IX_Activities_VehicleSchedule");

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

            migrationBuilder.AlterColumn<int>(
                name: "FamilyId",
                table: "Students",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "School",
                table: "Routes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Family",
                table: "Students",
                column: "FamilyId",
                principalTable: "Families",
                principalColumn: "FamilyId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
