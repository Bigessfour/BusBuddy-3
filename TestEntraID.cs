using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace BusBuddy.EntraIDTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🧪 Testing Microsoft Entra ID authentication for BusBuddy...");

            // Test different authentication methods
            await TestConnection("Default", "Authentication=Active Directory Default");
            await TestConnection("Interactive", "Authentication=Active Directory Interactive");

            Console.WriteLine("\n✅ Entra ID test completed!");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task TestConnection(string authType, string authParameter)
        {
            Console.WriteLine($"\n🔧 Testing {authType} authentication...");

            var connectionString = $"Server=tcp:busbuddy-server-sm2.database.windows.net,1433;" +
                                  $"Initial Catalog=BusBuddyDB;" +
                                  $"{authParameter};" +
                                  $"Encrypt=True;" +
                                  $"TrustServerCertificate=False;" +
                                  $"Connection Timeout=30;";

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("SELECT GETDATE() AS CurrentTime, USER_NAME() AS CurrentUser, DB_NAME() AS DatabaseName", connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    Console.WriteLine($"✅ {authType} authentication successful!");
                    Console.WriteLine($"   📅 Server Time: {reader["CurrentTime"]}");
                    Console.WriteLine($"   👤 Connected User: {reader["CurrentUser"]}");
                    Console.WriteLine($"   🗄️  Database: {reader["DatabaseName"]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {authType} authentication failed: {ex.Message}");

                if (ex.Message.Contains("firewall") || ex.Message.Contains("40615"))
                {
                    Console.WriteLine("💡 Firewall issue - ensure your IP is allowed");
                }
                else if (ex.Message.Contains("authentication") || ex.Message.Contains("login"))
                {
                    Console.WriteLine("💡 Authentication issue - ensure Entra ID is configured");
                }
            }
        }
    }
}
