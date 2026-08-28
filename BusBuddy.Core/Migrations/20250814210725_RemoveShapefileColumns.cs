using System;
using Microsoft.EntityFrameworkCore.Migrations;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace BusBuddy.Core.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BusBuddyDbContext))]
[Migration("20250814210725_RemoveShapefileColumns")]
public partial class RemoveShapefileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank - schema changes generated in designer file
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left blank - revert logic available in designer if needed
        }
    }
}
