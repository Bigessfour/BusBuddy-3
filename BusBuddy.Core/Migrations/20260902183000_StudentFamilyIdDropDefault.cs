using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Students.FamilyId had a legacy default of 0, which violates FK_Students_Family when no family row exists.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20260902183000_StudentFamilyIdDropDefault")]
public partial class StudentFamilyIdDropDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql("""ALTER TABLE "Students" ALTER COLUMN "FamilyId" DROP DEFAULT;""");
            migrationBuilder.Sql("""UPDATE "Students" SET "FamilyId" = NULL WHERE "FamilyId" = 0;""");
            return;
        }

        migrationBuilder.Sql("""
DECLARE @constraint sysname;
SELECT @constraint = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
INNER JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = 'Students' AND c.name = 'FamilyId';
IF @constraint IS NOT NULL
    EXEC(N'ALTER TABLE [Students] DROP CONSTRAINT [' + @constraint + N']');
""");
        migrationBuilder.Sql("UPDATE [Students] SET [FamilyId] = NULL WHERE [FamilyId] = 0;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (MigrationSql.IsNpgsql(migrationBuilder))
        {
            migrationBuilder.Sql("""ALTER TABLE "Students" ALTER COLUMN "FamilyId" SET DEFAULT 0;""");
            return;
        }

        migrationBuilder.Sql("ALTER TABLE [Students] ADD CONSTRAINT DF_Students_FamilyId DEFAULT 0 FOR [FamilyId];");
    }
}
