using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Azure.Identity;

class SimpleTest
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 Simple Entra ID authentication test for BusBuddy...\n");

        // Test Azure CLI credential (should work since we're authenticated)
        await TestAzureCliCredential();

        // Test connection string with DefaultAzureCredential
        await TestConnectionString();
    }

    static async Task TestAzureCliCredential()
    {
        Console.WriteLine("🔧 Testing Azure CLI credential...");
        try
        {
            var credential = new AzureCliCredential();
            var token = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }));

            Console.WriteLine("✅ Azure CLI credential successful");
            Console.WriteLine($"   Token expires: {token.ExpiresOn}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Azure CLI credential failed: {ex.Message}");
        }
        Console.WriteLine();
    }

    static async Task TestConnectionString()
    {
        Console.WriteLine("🔧 Testing SQL connection with Azure CLI credential...");

        var connectionString = "Server=busbuddy-server-sm2.database.windows.net;" +
                              "Database=BusBuddyDB;" +
                              "Authentication=Active Directory Default;" +
                              "Encrypt=true;" +
                              "TrustServerCertificate=false;" +
                              "Connection Timeout=30;";

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            Console.WriteLine("✅ SQL connection successful!");
            Console.WriteLine($"   Server version: {connection.ServerVersion}");
            Console.WriteLine($"   Database: {connection.Database}");

            // Test a simple query
            using var command = new SqlCommand("SELECT @@VERSION", connection);
            var result = await command.ExecuteScalarAsync();
            Console.WriteLine($"   SQL Version: {result?.ToString()?.Substring(0, 50)}...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ SQL connection failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
        }
    }
}
