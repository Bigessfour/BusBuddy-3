using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>Lift flag on the bus (specs/buses.md). Table name stays Vehicles.</summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260923020000_BusHasLift")]
public partial class BusHasLift : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasLift",
            table: "Vehicles",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HasLift", table: "Vehicles");
    }
}
