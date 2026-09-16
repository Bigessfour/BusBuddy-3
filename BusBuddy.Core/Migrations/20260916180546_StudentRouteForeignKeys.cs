using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    public partial class StudentRouteForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AmRouteId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PmRouteId",
                table: "Students",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_AmRouteId",
                table: "Students",
                column: "AmRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_PmRouteId",
                table: "Students",
                column: "PmRouteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Routes_AmRouteId",
                table: "Students",
                column: "AmRouteId",
                principalTable: "Routes",
                principalColumn: "RouteID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Students_Routes_PmRouteId",
                table: "Students",
                column: "PmRouteId",
                principalTable: "Routes",
                principalColumn: "RouteID",
                onDelete: ReferentialAction.SetNull);

            // Backfill the new keys from the existing route-name strings, case-insensitively to match
            // how DeleteRouteAsync and UpdateRouteAsync compare them.
            //
            // Only names resolving to exactly one route are backfilled. IX_Routes_DateRouteName makes
            // RouteName unique per *date*, so a name carried by more than one row cannot be attributed
            // to a single route, and guessing would silently move riders onto another date's route.
            // Those rows keep AmRouteId/PmRouteId NULL and still resolve by name until reassigned.
            migrationBuilder.Sql("""
                UPDATE "Students" s
                SET "AmRouteId" = r."RouteID"
                FROM "Routes" r
                WHERE s."AMRoute" IS NOT NULL
                  AND LOWER(s."AMRoute") = LOWER(r."RouteName")
                  AND NOT EXISTS (
                      SELECT 1 FROM "Routes" dup
                      WHERE LOWER(dup."RouteName") = LOWER(r."RouteName")
                        AND dup."RouteID" <> r."RouteID");
                """);

            migrationBuilder.Sql("""
                UPDATE "Students" s
                SET "PmRouteId" = r."RouteID"
                FROM "Routes" r
                WHERE s."PMRoute" IS NOT NULL
                  AND LOWER(s."PMRoute") = LOWER(r."RouteName")
                  AND NOT EXISTS (
                      SELECT 1 FROM "Routes" dup
                      WHERE LOWER(dup."RouteName") = LOWER(r."RouteName")
                        AND dup."RouteID" <> r."RouteID");
                """);

            // Snapshot-drift cleanup bundled with this migration. Skip when the catalog never
            // received AIInsights (truncated Docker histories still need the student FKs).
            migrationBuilder.Sql("""
                DO $migrate$
                BEGIN
                  IF to_regclass('public."AIInsights"') IS NOT NULL THEN
                    ALTER TABLE "AIInsights" ALTER COLUMN "Source" SET DEFAULT 'Ollama';
                  END IF;
                END
                $migrate$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Students_Routes_AmRouteId",
                table: "Students");

            migrationBuilder.DropForeignKey(
                name: "FK_Students_Routes_PmRouteId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_AmRouteId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Students_PmRouteId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "AmRouteId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "PmRouteId",
                table: "Students");

            migrationBuilder.Sql("""
                DO $migrate$
                BEGIN
                  IF to_regclass('public."AIInsights"') IS NOT NULL THEN
                    ALTER TABLE "AIInsights" ALTER COLUMN "Source" SET DEFAULT 'Grok-4';
                  END IF;
                END
                $migrate$;
                """);
        }
    }
}
