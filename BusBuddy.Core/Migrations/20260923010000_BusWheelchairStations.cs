using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>Wheelchair stations on the bus (specs/buses.md). Table name stays Vehicles.</summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260923010000_BusWheelchairStations")]
public partial class BusWheelchairStations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "WheelchairStations",
            table: "Vehicles",
            type: MigrationSql.IsNpgsql(migrationBuilder) ? "integer" : "int",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WheelchairStations", table: "Vehicles");
    }
}
