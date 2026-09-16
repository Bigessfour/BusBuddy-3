using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Operational log for clerk-confirmed student deletions. specs/students.md: Mistake, Moved, or
/// Not attending. No FK to Students — the roster row is already gone.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260916010000_StudentDeletionLogs")]
public partial class StudentDeletionLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StudentDeletionLogs",
            columns: table => new
            {
                StudentDeletionLogId = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StudentId = table.Column<int>(nullable: false),
                StudentNumber = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 20), maxLength: 20, nullable: true),
                SchoolYear = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 9), maxLength: 9, nullable: true),
                Reason = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 32), maxLength: 32, nullable: false),
                Notes = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 200), maxLength: 200, nullable: true),
                WasActive = table.Column<bool>(type: MigrationSql.BoolType(migrationBuilder), nullable: false),
                ScheduleCount = table.Column<int>(nullable: false),
                TransferCount = table.Column<int>(nullable: false),
                RiderExceptionCount = table.Column<int>(nullable: false),
                DeletedUtc = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StudentDeletionLogs", x => x.StudentDeletionLogId);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StudentDeletionLogs_StudentId",
            table: "StudentDeletionLogs",
            column: "StudentId");

        migrationBuilder.CreateIndex(
            name: "IX_StudentDeletionLogs_DeletedUtc",
            table: "StudentDeletionLogs",
            column: "DeletedUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StudentDeletionLogs");
    }
}
