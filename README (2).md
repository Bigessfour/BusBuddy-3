# BusBuddy with Inventory Dashboard

This project includes a minimal interface for Microsoft SQL Server Express to display inventory data on a dashboard.

## Setup Instructions

### Prerequisites

1. **Microsoft SQL Server Express**
   - Download and install the latest version from [Microsoft's website](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
   - Make sure to note your instance name (default is usually `.\SQLEXPRESS` or `localhost\SQLEXPRESS`)

2. **SQL Server Management Studio (SSMS)**
   - Download and install from [Microsoft's website](https://docs.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)
   - This will be used to manage your SQL Server instance

3. **.NET 8.0 SDK**
   - Ensure you have the .NET 8.0 SDK installed

### Database Setup

The application will automatically create the necessary databases and tables when it runs. However, if you want to manually set up the database:

1. Open SQL Server Management Studio
2. Connect to your SQL Server Express instance
3. Run the `InitializeInventoryDB.sql` script included in the project

### Configuration

The application uses Windows Authentication to connect to SQL Server Express. The connection string is configured in `App.Config`. If your SQL Server instance name is different from the default, update the connection string:

```xml
<add name="BusBuddy" connectionString="Server=YOUR_SERVER_NAME;Database=InventoryDB;Trusted_Connection=True;TrustServerCertificate=True;" providerName="System.Data.SqlClient" />
```

### Building and Running the Application

1. Open a command prompt in the project directory
2. Run `dotnet build` to build the application
3. Run `dotnet run` to start the application
4. When prompted, select "Yes" to open the Inventory Dashboard

## Features

- **Inventory Dashboard**: Displays a table of items with their ID, name, quantity, and price
- **Automatic Database Setup**: Creates the necessary databases and tables if they don't exist
- **Sample Data**: Includes sample inventory items for testing

## Project Structure

- **Models/Item.cs**: Defines the Item model for the Items table
- **Data/DatabaseContext.cs**: Entity Framework Core DbContext for database access
- **Services/ItemService.cs**: Service for retrieving and managing inventory items
- **Forms/InventoryDashboard.cs**: Windows Forms dashboard to display inventory items
- **Utilities/DatabaseUtility.cs**: Utility methods for database operations
- **InitializeInventoryDB.sql**: SQL script to create the InventoryDB database and Items table

## Troubleshooting

- **Connection Issues**: Ensure SQL Server Express is running and the connection string in App.Config is correct
- **Build Errors**: Make sure you have the .NET 8.0 SDK installed
- **Missing Tables**: The application should create tables automatically, but you can run the SQL script manually if needed
