using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Special-needs transport flags on students and routes.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260902140000_SpecialNeedsTransportFlags")]
public partial class SpecialNeedsTransportFlags : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "RequiresSpecialNeedsBus",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "RequiresWheelchair",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "RequiresSeatBelt",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "RequiresAide",
            table: "Students",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsSpecialNeedsRoute",
            table: "Routes",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql(
                "UPDATE \"Students\" SET \"RequiresSpecialNeedsBus\" = TRUE WHERE \"SpecialNeeds\" IS NOT NULL AND BTRIM(\"SpecialNeeds\") <> ''");
            migrationBuilder.Sql(
                "UPDATE \"Routes\" SET \"IsSpecialNeedsRoute\" = TRUE WHERE \"RouteName\" ILIKE '%Special Needs%'");
        }
        else
        {
            migrationBuilder.Sql(
                "UPDATE Students SET RequiresSpecialNeedsBus = 1 WHERE SpecialNeeds IS NOT NULL AND LTRIM(RTRIM(SpecialNeeds)) <> ''");
            migrationBuilder.Sql(
                "UPDATE Routes SET IsSpecialNeedsRoute = 1 WHERE RouteName LIKE '%Special Needs%'");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RequiresSpecialNeedsBus", table: "Students");
        migrationBuilder.DropColumn(name: "RequiresWheelchair", table: "Students");
        migrationBuilder.DropColumn(name: "RequiresSeatBelt", table: "Students");
        migrationBuilder.DropColumn(name: "RequiresAide", table: "Students");
        migrationBuilder.DropColumn(name: "IsSpecialNeedsRoute", table: "Routes");
    }
}
