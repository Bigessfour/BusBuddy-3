using Microsoft.EntityFrameworkCore.Migrations;

namespace BusBuddy.Core.Migrations;

/// <summary>
/// Migration Step 3: Rename primary key column VehicleId -> BusId on Vehicles (Buses) table.
/// Uses RenameColumn to preserve data, drops and re-adds PK, updates related index names.
/// NOTE: Additional foreign key column renames should be added in subsequent focused migrations.
/// </summary>
public partial class RenameVehicleIdToBusId : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Table name in database is Vehicles (per modelBuilder mapping), not Buses.
		// Drop existing PK (VehicleId)
		migrationBuilder.DropPrimaryKey(
			name: "PK_Vehicles",
			table: "Vehicles");

		// Rename the PK column
		migrationBuilder.RenameColumn(
			name: "VehicleId",
			table: "Vehicles",
			newName: "BusId");

		// Re-add primary key on new column
		migrationBuilder.AddPrimaryKey(
			name: "PK_Vehicles",
			table: "Vehicles",
			column: "BusId");

		// Rename indexes in dependent tables that include VehicleId in the name only where the physical column stays VehicleId for now.
		// For ActivitySchedule / Maintenance / Activities etc., column names still VehicleId; renaming those columns will occur in later migrations.
		// Example index rename (Activities vehicle FK index) if previously created with VehicleId naming pattern.
		migrationBuilder.RenameIndex(
			name: "IX_Activities_VehicleId",
			table: "Activities",
			newName: "IX_Activities_BusId");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Drop new PK
		migrationBuilder.DropPrimaryKey(
			name: "PK_Vehicles",
			table: "Vehicles");

		// Rename column back
		migrationBuilder.RenameColumn(
			name: "BusId",
			table: "Vehicles",
			newName: "VehicleId");

		// Re-add original PK
		migrationBuilder.AddPrimaryKey(
			name: "PK_Vehicles",
			table: "Vehicles",
			column: "VehicleId");

		// Revert index rename
		migrationBuilder.RenameIndex(
			name: "IX_Activities_BusId",
			table: "Activities",
			newName: "IX_Activities_VehicleId");
	}
}
