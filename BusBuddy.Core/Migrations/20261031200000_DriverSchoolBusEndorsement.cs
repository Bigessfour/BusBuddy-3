using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Explicit school-bus endorsement flag for student-route / trip assignment (#114).
/// Backfills from free-text Endorsements when it looks like an S / school-bus mark.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20261031200000_DriverSchoolBusEndorsement")]
public partial class DriverSchoolBusEndorsement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "SchoolBusEndorsement",
            table: "Drivers",
            type: MigrationSql.BoolType(migrationBuilder),
            nullable: false,
            defaultValue: false);

        // Conservative backfill: S token or school-bus wording in Endorsements text.
        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql(
                """
                UPDATE "Drivers"
                SET "SchoolBusEndorsement" = TRUE
                WHERE "Endorsements" IS NOT NULL
                  AND (
                    UPPER(TRIM("Endorsements")) = 'S'
                    OR UPPER("Endorsements") ~ '(^|[,;/\s|])S([,;/\s|]|$)'
                    OR UPPER("Endorsements") LIKE '%SCHOOL BUS%'
                    OR UPPER("Endorsements") LIKE '%SCHOOL-BUS%'
                    OR UPPER("Endorsements") ~ '(^|[,;/\s|])SB([,;/\s|]|$)'
                  );
                """);
        }
        else
        {
            migrationBuilder.Sql(
                """
                UPDATE [Drivers]
                SET [SchoolBusEndorsement] = 1
                WHERE [Endorsements] IS NOT NULL
                  AND (
                    UPPER(LTRIM(RTRIM([Endorsements]))) = 'S'
                    OR UPPER([Endorsements]) LIKE '%SCHOOL BUS%'
                    OR UPPER([Endorsements]) LIKE '%SCHOOL-BUS%'
                    OR ',' + REPLACE(REPLACE(REPLACE(UPPER([Endorsements]), ' ', ','), ';', ','), '/', ',') + ',' LIKE '%,S,%'
                    OR ',' + REPLACE(REPLACE(REPLACE(UPPER([Endorsements]), ' ', ','), ';', ','), '/', ',') + ',' LIKE '%,SB,%'
                  );
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SchoolBusEndorsement", table: "Drivers");
    }
}
