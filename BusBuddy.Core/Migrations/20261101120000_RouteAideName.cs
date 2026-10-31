using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Clerk-entered aide name on a special-needs route (#118). Not a driver FK.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20261101120000_RouteAideName")]
public partial class RouteAideName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AideName",
            table: "Routes",
            type: MigrationSql.StringType(migrationBuilder, 100),
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AideName", table: "Routes");
    }
}
