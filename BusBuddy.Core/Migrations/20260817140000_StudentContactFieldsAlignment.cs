using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Student contact fields (parent email/cell, emergency name) + school Destination params +
/// inter-district StudentSchoolTransfers + seed Wiley campus.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260817140000_StudentContactFieldsAlignment")]
public partial class StudentContactFieldsAlignment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ParentEmail",
            table: "Students",
            type: MigrationSql.StringType(migrationBuilder, 100),
                maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CellPhone",
            table: "Students",
            type: MigrationSql.StringType(migrationBuilder, 20),
                maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EmergencyContactName",
            table: "Students",
            type: MigrationSql.StringType(migrationBuilder, 100),
                maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "HasMedicalNeeds",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
                nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "DestinationId",
            table: "Students",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminContactName",
            table: "Destinations",
            type: MigrationSql.StringType(migrationBuilder, 100),
                maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminPhone",
            table: "Destinations",
            type: MigrationSql.StringType(migrationBuilder, 20),
                maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminEmail",
            table: "Destinations",
            type: MigrationSql.StringType(migrationBuilder, 100),
                maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DistrictName",
            table: "Destinations",
            type: MigrationSql.StringType(migrationBuilder, 150),
                maxLength: 150,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GradeMin",
            table: "Destinations",
            type: MigrationSql.StringType(migrationBuilder, 20),
                maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GradeMax",
            table: "Destinations",
            type: MigrationSql.StringType(migrationBuilder, 20),
                maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AgeMinYears",
            table: "Destinations",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AgeMaxYears",
            table: "Destinations",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "StudentSchoolTransfers",
            columns: table => new
            {
                TransferId = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StudentId = table.Column<int>(nullable: false),
                FromDestinationId = table.Column<int>(nullable: false),
                ToDestinationId = table.Column<int>(nullable: false),
                PickupTime = table.Column<TimeSpan>(nullable: true),
                DropoffTime = table.Column<TimeSpan>(nullable: true),
                PickupAddress = table.Column<string>(maxLength: 300, nullable: true),
                DropoffAddress = table.Column<string>(maxLength: 300, nullable: true),
                PickupLatitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                PickupLongitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                DropoffLatitude = table.Column<decimal>(type: "decimal(10,8)", nullable: true),
                DropoffLongitude = table.Column<decimal>(type: "decimal(11,8)", nullable: true),
                EffectiveDate = table.Column<DateTime>(nullable: true),
                EndDate = table.Column<DateTime>(nullable: true),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                Notes = table.Column<string>(maxLength: 1000, nullable: true),
                CreatedDate = table.Column<DateTime>(nullable: false),
                CreatedBy = table.Column<string>(maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudentSchoolTransfers", x => x.TransferId);
                table.ForeignKey(
                    name: "FK_StudentSchoolTransfers_Students_StudentId",
                    column: x => x.StudentId,
                    principalTable: "Students",
                    principalColumn: "StudentId",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StudentSchoolTransfers_Destinations_FromDestinationId",
                    column: x => x.FromDestinationId,
                    principalTable: "Destinations",
                    principalColumn: "DestinationId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StudentSchoolTransfers_Destinations_ToDestinationId",
                    column: x => x.ToDestinationId,
                    principalTable: "Destinations",
                    principalColumn: "DestinationId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Students_DestinationId",
            table: "Students",
            column: "DestinationId");

        migrationBuilder.AddForeignKey(
            name: "FK_Students_Destinations_DestinationId",
            table: "Students",
            column: "DestinationId",
            principalTable: "Destinations",
            principalColumn: "DestinationId",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.CreateIndex(
            name: "IX_StudentSchoolTransfers_Student",
            table: "StudentSchoolTransfers",
            column: "StudentId");

        migrationBuilder.CreateIndex(
            name: "IX_StudentSchoolTransfers_Active",
            table: "StudentSchoolTransfers",
            column: "IsActive");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Students_Destinations_DestinationId",
            table: "Students");

        migrationBuilder.DropTable(name: "StudentSchoolTransfers");

        migrationBuilder.DropIndex(name: "IX_Students_DestinationId", table: "Students");

        migrationBuilder.DropColumn(name: "ParentEmail", table: "Students");
        migrationBuilder.DropColumn(name: "CellPhone", table: "Students");
        migrationBuilder.DropColumn(name: "EmergencyContactName", table: "Students");
        migrationBuilder.DropColumn(name: "HasMedicalNeeds", table: "Students");
        migrationBuilder.DropColumn(name: "DestinationId", table: "Students");

        migrationBuilder.DropColumn(name: "AdminContactName", table: "Destinations");
        migrationBuilder.DropColumn(name: "AdminPhone", table: "Destinations");
        migrationBuilder.DropColumn(name: "AdminEmail", table: "Destinations");
        migrationBuilder.DropColumn(name: "DistrictName", table: "Destinations");
        migrationBuilder.DropColumn(name: "GradeMin", table: "Destinations");
        migrationBuilder.DropColumn(name: "GradeMax", table: "Destinations");
        migrationBuilder.DropColumn(name: "AgeMinYears", table: "Destinations");
        migrationBuilder.DropColumn(name: "AgeMaxYears", table: "Destinations");
    }
}
