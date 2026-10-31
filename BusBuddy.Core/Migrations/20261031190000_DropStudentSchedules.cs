using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Attendance / student-on-schedule rows are out of product scope (#109).
/// Clerk roster assignment stays on Student.AmRouteId / PmRouteId.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20261031190000_DropStudentSchedules")]
public partial class DropStudentSchedules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StudentSchedules");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: do not recreate the attendance table.
    }
}
