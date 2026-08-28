using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Driver employment fields + DriverTrainingRecords sub-module for Colorado CDE 2024-25 license/training matrix.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260817150000_DriverTrainingSubmodule")]
public partial class DriverTrainingSubmodule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "EmployingDistrict",
            table: "Drivers",
            type: MigrationSql.StringType(migrationBuilder, 150),
                maxLength: 150,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "EmploymentEndDate",
            table: "Drivers",
            type: MigrationSql.DateTimeType(migrationBuilder),
                nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DutyCategory",
            table: "Drivers",
            type: MigrationSql.StringType(migrationBuilder, 20),
                maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VehicleCategory",
            table: "Drivers",
            type: MigrationSql.StringType(migrationBuilder, 60),
                maxLength: 60,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CdlRestrictions",
            table: "Drivers",
            type: MigrationSql.StringType(migrationBuilder, 50),
                maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MedicalFormType",
            table: "Drivers",
            type: MigrationSql.StringType(migrationBuilder, 40),
                maxLength: 40,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DriverTrainingRecords",
            columns: table => new
            {
                TrainingRecordId = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DriverId = table.Column<int>(nullable: false),
                RequirementCode = table.Column<string>(maxLength: 80, nullable: false),
                RequirementName = table.Column<string>(maxLength: 200, nullable: false),
                CompletedDate = table.Column<DateTime>(nullable: true),
                ExpiryDate = table.Column<DateTime>(nullable: true),
                IsRequired = table.Column<bool>(nullable: false, defaultValue: true),
                IsApplicable = table.Column<bool>(nullable: false, defaultValue: true),
                CertificateOrReference = table.Column<string>(maxLength: 100, nullable: true),
                ProviderOrInstructor = table.Column<string>(maxLength: 100, nullable: true),
                Notes = table.Column<string>(maxLength: 1000, nullable: true),
                CreatedDate = table.Column<DateTime>(nullable: false),
                CreatedBy = table.Column<string>(maxLength: 100, nullable: true),
                UpdatedDate = table.Column<DateTime>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DriverTrainingRecords", x => x.TrainingRecordId);
                table.ForeignKey(
                    name: "FK_DriverTrainingRecords_Drivers_DriverId",
                    column: x => x.DriverId,
                    principalTable: "Drivers",
                    principalColumn: "DriverID",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DriverTrainingRecords_Driver",
            table: "DriverTrainingRecords",
            column: "DriverId");

        migrationBuilder.CreateIndex(
            name: "IX_DriverTrainingRecords_Driver_Code",
            table: "DriverTrainingRecords",
            columns: new[] { "DriverId", "RequirementCode" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "DriverTrainingRecords");
        migrationBuilder.DropColumn(name: "EmployingDistrict", table: "Drivers");
        migrationBuilder.DropColumn(name: "EmploymentEndDate", table: "Drivers");
        migrationBuilder.DropColumn(name: "DutyCategory", table: "Drivers");
        migrationBuilder.DropColumn(name: "VehicleCategory", table: "Drivers");
        migrationBuilder.DropColumn(name: "CdlRestrictions", table: "Drivers");
        migrationBuilder.DropColumn(name: "MedicalFormType", table: "Drivers");
    }
}
