using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FamilyId",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Families",
                columns: table => new
                {
                    FamilyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentGuardian = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: false, defaultValue: ""),
                    Address = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 200), maxLength: 200, nullable: false, defaultValue: ""),
                    City = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 50), maxLength: 50, nullable: false, defaultValue: ""),
                    County = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 50), maxLength: 50, nullable: false, defaultValue: ""),
                    HomePhone = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 20), maxLength: 20, nullable: true),
                    CellPhone = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 20), maxLength: 20, nullable: true),
                    EmergencyContact = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: true),
                    JointParent = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false, defaultValueSql: MigrationSql.UtcNow(migrationBuilder)),
                    CreatedBy = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: true),
                    UpdatedDate = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: true),
                    UpdatedBy = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Families", x => x.FamilyId);
                });

            migrationBuilder.CreateTable(
                name: "SportsEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1")
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventName = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 200), maxLength: 200, nullable: false, defaultValue: ""),
                    StartTime = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                    EndTime = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                    Location = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 500), maxLength: 500, nullable: false, defaultValue: ""),
                    TeamSize = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: true),
                    DriverId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 50), maxLength: 50, nullable: false, defaultValue: ""),
                    SafetyNotes = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 1000), maxLength: 1000, nullable: false, defaultValue: ""),
                    Sport = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 100), maxLength: 100, nullable: false, defaultValue: ""),
                    IsHomeGame = table.Column<bool>(type: MigrationSql.BoolType(migrationBuilder), nullable: false),
                    EmergencyContact = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 500), maxLength: 500, nullable: false, defaultValue: ""),
                    WeatherConditions = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 200), maxLength: 200, nullable: false, defaultValue: ""),
                    CreatedAt = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                    RowVersion = table.Column<byte[]>(nullable: true),
                    IsDeleted = table.Column<bool>(type: MigrationSql.BoolType(migrationBuilder), nullable: false),
                    DeletedAt = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: true),
                    CreatedBy = table.Column<string>(type: MigrationSql.StringType(migrationBuilder), nullable: true),
                    UpdatedBy = table.Column<string>(type: MigrationSql.StringType(migrationBuilder), nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SportsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SportsEvents_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "DriverID");
                    table.ForeignKey(
                        name: "FK_SportsEvents_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Students_FamilyId",
                table: "Students",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_Families_City",
                table: "Families",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Families_ParentAddress",
                table: "Families",
                columns: new[] { "ParentGuardian", "Address" });

            migrationBuilder.CreateIndex(
                name: "IX_SportsEvents_DriverId",
                table: "SportsEvents",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_SportsEvents_VehicleId",
                table: "SportsEvents",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Family",
                table: "Students",
                column: "FamilyId",
                principalTable: "Families",
                principalColumn: "FamilyId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Family",
                table: "Students");

            migrationBuilder.DropTable(
                name: "Families");

            migrationBuilder.DropTable(
                name: "SportsEvents");

            migrationBuilder.DropIndex(
                name: "IX_Students_FamilyId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "Students");
        }
    }
}
