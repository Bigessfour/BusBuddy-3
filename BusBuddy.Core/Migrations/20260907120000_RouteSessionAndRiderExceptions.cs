using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Explicit route session (AM/PM/Transfer/SpecialNeeds) and same-day rider exceptions.
/// Exceptions do not rewrite published stops.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260907120000_RouteSessionAndRiderExceptions")]
public partial class RouteSessionAndRiderExceptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var sessionType = MigrationSql.StringType(migrationBuilder, 20);
        migrationBuilder.AddColumn<string>(
            name: "Session",
            table: "Routes",
            type: sessionType,
            maxLength: 20,
            nullable: false,
            defaultValue: "AM");

        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql(
                """UPDATE "Routes" SET "Session" = 'SpecialNeeds' WHERE "IsSpecialNeedsRoute" = TRUE;""");
            migrationBuilder.Sql(
                """UPDATE "Routes" SET "Session" = 'Transfer' WHERE "Session" = 'AM' AND ("Description" ILIKE '%Transfer%' OR "RouteName" ILIKE '%Transfer%');""");
            migrationBuilder.Sql(
                """UPDATE "Routes" SET "Session" = 'PM' WHERE "Session" = 'AM' AND "RouteName" ILIKE '%-PM';""");
        }
        else
        {
            migrationBuilder.Sql(
                "UPDATE Routes SET Session = 'SpecialNeeds' WHERE IsSpecialNeedsRoute = 1;");
            migrationBuilder.Sql(
                "UPDATE Routes SET Session = 'Transfer' WHERE Session = 'AM' AND (Description LIKE '%Transfer%' OR RouteName LIKE '%Transfer%');");
            migrationBuilder.Sql(
                "UPDATE Routes SET Session = 'PM' WHERE Session = 'AM' AND RouteName LIKE '%-PM';");
        }

        migrationBuilder.CreateIndex(
            name: "IX_Routes_Session",
            table: "Routes",
            column: "Session");

        migrationBuilder.CreateTable(
            name: "RouteRiderExceptions",
            columns: table => new
            {
                RouteRiderExceptionId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1")
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RouteId = table.Column<int>(type: "int", nullable: false),
                StudentId = table.Column<int>(type: "int", nullable: false),
                ExceptionDate = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false),
                Reason = table.Column<string>(type: MigrationSql.StringType(migrationBuilder, 200), maxLength: 200, nullable: true),
                CreatedDate = table.Column<DateTime>(type: MigrationSql.DateTimeType(migrationBuilder), nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RouteRiderExceptions", x => x.RouteRiderExceptionId);
                table.ForeignKey(
                    name: "FK_RouteRiderExceptions_Route",
                    column: x => x.RouteId,
                    principalTable: "Routes",
                    principalColumn: "RouteID",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RouteRiderExceptions_Student",
                    column: x => x.StudentId,
                    principalTable: "Students",
                    principalColumn: "StudentId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RouteRiderExceptions_RouteStudentDate",
            table: "RouteRiderExceptions",
            columns: new[] { "RouteId", "StudentId", "ExceptionDate" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "RouteRiderExceptions");
        migrationBuilder.DropIndex(name: "IX_Routes_Session", table: "Routes");
        migrationBuilder.DropColumn(name: "Session", table: "Routes");
    }
}
