using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class FixDatabaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Family",
                table: "Students");

            migrationBuilder.RenameIndex(
                name: "IX_TripEvents_VehicleSchedule",
                table: "TripEvents",
                newName: "IX_TripEvents_BusSchedule");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_VehicleSchedule",
                table: "Activities",
                newName: "IX_Activities_BusSchedule");

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
                name: "FK_RouteAssignments_Guardians_GuardianId",
                table: "RouteAssignments",
                column: "GuardianId",
                principalTable: "Guardians",
                principalColumn: "Id");

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
                name: "FK_RouteAssignments_Guardians_GuardianId",
                table: "RouteAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Family",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Guardians");

            migrationBuilder.DropIndex(
                name: "IX_RouteAssignments_GuardianId",
                table: "RouteAssignments");

            migrationBuilder.DropColumn(
                name: "GuardianId",
                table: "RouteAssignments");

            migrationBuilder.RenameIndex(
                name: "IX_TripEvents_BusSchedule",
                table: "TripEvents",
                newName: "IX_TripEvents_VehicleSchedule");

            migrationBuilder.RenameIndex(
                name: "IX_Activities_BusSchedule",
                table: "Activities",
                newName: "IX_Activities_VehicleSchedule");

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
