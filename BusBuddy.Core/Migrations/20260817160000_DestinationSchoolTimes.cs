using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Destination school StartTime / DismissalTime for route determination (spec 008).
/// Apply with <c>dotnet ef database update --project BusBuddy.Core --startup-project BusBuddy.Core</c>
/// (Mac Docker Postgres or Windows SQL Server). See Documentation/DATABASE-CONFIGURATION.md.
/// Snapshot may lag older migrations; this Up/Down is authoritative for these columns.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260817160000_DestinationSchoolTimes")]
public partial class DestinationSchoolTimes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<TimeSpan>(
            name: "StartTime",
            table: "Destinations",
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "DismissalTime",
            table: "Destinations",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "StartTime", table: "Destinations");
        migrationBuilder.DropColumn(name: "DismissalTime", table: "Destinations");
    }
}
