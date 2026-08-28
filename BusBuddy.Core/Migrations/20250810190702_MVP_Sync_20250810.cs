using Microsoft.EntityFrameworkCore.Migrations;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BusBuddyDbContext))]
[Migration("20250810190702_MVP_Sync_20250810")]
public partial class MVP_Sync_20250810 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FamilyId",
                table: "Students",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "FamilyId",
                table: "Students",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
