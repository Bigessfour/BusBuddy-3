using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Clerk driveway/gate pin. Address Validation of the same street must not move it.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260923030000_StudentHomePickupClerkAdjusted")]
public partial class StudentHomePickupClerkAdjusted : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HomePickupClerkAdjusted",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HomePickupClerkAdjusted", table: "Students");
    }
}
