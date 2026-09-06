using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Encoded drive polylines plus stop lists exceed the old 4000-character cap.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260906220000_WidenRouteWaypointsJson")]
public partial class WidenRouteWaypointsJson : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var type = MigrationSql.StringType(migrationBuilder);
        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql($"""ALTER TABLE "Routes" ALTER COLUMN "WaypointsJson" TYPE {type};""");
            return;
        }

        migrationBuilder.Sql($"ALTER TABLE [Routes] ALTER COLUMN [WaypointsJson] {type} NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        var type = MigrationSql.StringType(migrationBuilder, 4000);
        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql($"""ALTER TABLE "Routes" ALTER COLUMN "WaypointsJson" TYPE {type};""");
            return;
        }

        migrationBuilder.Sql($"ALTER TABLE [Routes] ALTER COLUMN [WaypointsJson] {type} NULL;");
    }
}
