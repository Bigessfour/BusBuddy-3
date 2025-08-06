using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Test Azure SQL connection
        var userId = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
        var password = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");

        Console.WriteLine($"Testing Azure SQL Connection...");
        Console.WriteLine($"User: {userId}");
        Console.WriteLine($"Password Set: {!string.IsNullOrEmpty(password)}");

        var connectionString = $"Server=tcp:busbuddy-server-sm2.database.windows.net,1433;Initial Catalog=BusBuddyDB;Persist Security Info=False;User ID={userId};Password={password};MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("✅ Connection successful!");

            // Test if database exists
            using var command = new SqlCommand("SELECT DB_NAME()", connection);
            var dbName = await command.ExecuteScalarAsync();
            Console.WriteLine($"✅ Connected to database: {dbName}");

            // Test if we can create tables (basic permission check)
            using var testCommand = new SqlCommand("SELECT 1", connection);
            await testCommand.ExecuteScalarAsync();
            Console.WriteLine("✅ Basic query successful!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Connection failed: {ex.Message}");

            if (ex is SqlException sqlEx)
            {
                Console.WriteLine($"SQL Error Number: {sqlEx.Number}");
                Console.WriteLine($"SQL Error State: {sqlEx.State}");
                Console.WriteLine($"SQL Error Class: {sqlEx.Class}");
            }
        }
    }
}
