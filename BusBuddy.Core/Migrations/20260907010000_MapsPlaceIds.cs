using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Persist Google place ids on students (durable refresh handle; lat/lng remain TTL-cached).
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260907010000_MapsPlaceIds")]
public partial class MapsPlaceIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PlaceId",
            table: "Students",
            type: MigrationSql.StringType(migrationBuilder, 256),
            maxLength: 256,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PlaceId", table: "Students");
    }
}
