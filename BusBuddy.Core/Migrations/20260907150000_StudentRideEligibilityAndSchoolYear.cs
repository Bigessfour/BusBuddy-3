using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// specs/students.md first-class fields: explicit AM/PM ride eligibility and SchoolYear.
/// Also indexes the AMRoute/PMRoute name columns and stops a Family delete (or a student purge)
/// from cascading away students and their transfer history.
/// </summary>
/// <remarks>
/// The attributes are inline rather than in a scaffolded <c>.Designer.cs</c> because
/// <c>.gitignore</c> excludes <c>*.Designer.cs</c> — a generated designer would not be committed
/// and EF would never discover this migration.
/// </remarks>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260907150000_StudentRideEligibilityAndSchoolYear")]
public partial class StudentRideEligibilityAndSchoolYear : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "RidesAm",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "RidesPm",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "SchoolYear",
            table: "Students",
            type: MigrationSql.StringType(migrationBuilder, 9),
            maxLength: 9,
            nullable: true);

        // Eligibility was previously inferred from the route-name columns. Seed the explicit flags
        // from that inference once so existing rosters keep their AM/PM behavior.
        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql(
                "UPDATE \"Students\" SET \"RidesAm\" = TRUE WHERE \"AMRoute\" IS NOT NULL AND BTRIM(\"AMRoute\") <> '';");
            migrationBuilder.Sql(
                "UPDATE \"Students\" SET \"RidesPm\" = TRUE WHERE \"PMRoute\" IS NOT NULL AND BTRIM(\"PMRoute\") <> '';");
            migrationBuilder.Sql(
                """
                UPDATE "Students"
                SET "SchoolYear" = CASE
                        WHEN EXTRACT(MONTH FROM CURRENT_DATE) >= 7
                            THEN CONCAT(EXTRACT(YEAR FROM CURRENT_DATE)::int, '-', EXTRACT(YEAR FROM CURRENT_DATE)::int + 1)
                        ELSE CONCAT(EXTRACT(YEAR FROM CURRENT_DATE)::int - 1, '-', EXTRACT(YEAR FROM CURRENT_DATE)::int)
                    END
                WHERE "SchoolYear" IS NULL;
                """);
        }
        else
        {
            migrationBuilder.Sql(
                "UPDATE Students SET RidesAm = 1 WHERE AMRoute IS NOT NULL AND LTRIM(RTRIM(AMRoute)) <> '';");
            migrationBuilder.Sql(
                "UPDATE Students SET RidesPm = 1 WHERE PMRoute IS NOT NULL AND LTRIM(RTRIM(PMRoute)) <> '';");
            migrationBuilder.Sql(
                """
                UPDATE Students
                SET SchoolYear = CASE
                        WHEN MONTH(GETUTCDATE()) >= 7
                            THEN CONCAT(YEAR(GETUTCDATE()), '-', YEAR(GETUTCDATE()) + 1)
                        ELSE CONCAT(YEAR(GETUTCDATE()) - 1, '-', YEAR(GETUTCDATE()))
                    END
                WHERE SchoolYear IS NULL;
                """);
        }

        // AMRoute/PMRoute are route *name* strings with no FK; without these indexes every
        // roster-by-route read and rename audit is a table scan.
        migrationBuilder.CreateIndex(
            name: "IX_Students_AMRoute",
            table: "Students",
            column: "AMRoute");

        migrationBuilder.CreateIndex(
            name: "IX_Students_PMRoute",
            table: "Students",
            column: "PMRoute");

        migrationBuilder.CreateIndex(
            name: "IX_Students_SchoolYear",
            table: "Students",
            column: "SchoolYear");

        // specs/students.md: "MUST NOT delete a student to end service. Archive or set inactive so
        // history and route versions remain." Cascade here let a Family delete wipe its students.
        migrationBuilder.DropForeignKey(
            name: "FK_Students_Family",
            table: "Students");

        migrationBuilder.AddForeignKey(
            name: "FK_Students_Family",
            table: "Students",
            column: "FamilyId",
            principalTable: "Families",
            principalColumn: "FamilyId",
            onDelete: ReferentialAction.Restrict);

        // A transfer is assignment history and must outlive any student row removal.
        migrationBuilder.DropForeignKey(
            name: "FK_StudentSchoolTransfers_Students_StudentId",
            table: "StudentSchoolTransfers");

        migrationBuilder.AddForeignKey(
            name: "FK_StudentSchoolTransfers_Students_StudentId",
            table: "StudentSchoolTransfers",
            column: "StudentId",
            principalTable: "Students",
            principalColumn: "StudentId",
            onDelete: ReferentialAction.Restrict);

        // A schedule assignment is history on the same footing as a transfer.
        migrationBuilder.DropForeignKey(
            name: "FK_StudentSchedules_Student",
            table: "StudentSchedules");

        migrationBuilder.AddForeignKey(
            name: "FK_StudentSchedules_Student",
            table: "StudentSchedules",
            column: "StudentId",
            principalTable: "Students",
            principalColumn: "StudentId",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_StudentSchedules_Student",
            table: "StudentSchedules");

        migrationBuilder.AddForeignKey(
            name: "FK_StudentSchedules_Student",
            table: "StudentSchedules",
            column: "StudentId",
            principalTable: "Students",
            principalColumn: "StudentId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropForeignKey(
            name: "FK_StudentSchoolTransfers_Students_StudentId",
            table: "StudentSchoolTransfers");

        migrationBuilder.AddForeignKey(
            name: "FK_StudentSchoolTransfers_Students_StudentId",
            table: "StudentSchoolTransfers",
            column: "StudentId",
            principalTable: "Students",
            principalColumn: "StudentId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropForeignKey(
            name: "FK_Students_Family",
            table: "Students");

        migrationBuilder.AddForeignKey(
            name: "FK_Students_Family",
            table: "Students",
            column: "FamilyId",
            principalTable: "Families",
            principalColumn: "FamilyId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.DropIndex(name: "IX_Students_SchoolYear", table: "Students");
        migrationBuilder.DropIndex(name: "IX_Students_PMRoute", table: "Students");
        migrationBuilder.DropIndex(name: "IX_Students_AMRoute", table: "Students");

        migrationBuilder.DropColumn(name: "SchoolYear", table: "Students");
        migrationBuilder.DropColumn(name: "RidesPm", table: "Students");
        migrationBuilder.DropColumn(name: "RidesAm", table: "Students");
    }
}
