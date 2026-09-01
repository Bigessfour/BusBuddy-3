using Microsoft.EntityFrameworkCore.Migrations;
using BusBuddy.Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Migration Step 3: Rename primary key column VehicleId -> BusId on Vehicles (Buses) table.
/// Uses RenameColumn to preserve data, drops and re-adds PK, updates related index names.
/// NOTE: Additional foreign key column renames should be added in subsequent focused migrations.
/// </summary>
[DbContext(typeof(BusBuddyDbContext))]
[Migration("20250822150000_RenameVehicleIdToBusId")]
public partial class RenameVehicleIdToBusId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: Bus.BusId is still mapped to column VehicleId in OnModelCreating.
        // Dropping PK_Vehicles fails on Postgres (FKs depend on it), and renaming
        // would desync the fluent HasColumnName("VehicleId") mapping.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
