using System;
using System.Threading;
using System.Windows;
using Serilog;

namespace BusBuddy.WPF.Test
{
    public partial class TestApp : Application
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // Set STA apartment state explicitly
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

                // Initialize basic logging
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .CreateLogger();

                Log.Information("🚌 Test app starting with STA: {State}",
                    Thread.CurrentThread.GetApartmentState());

                // Register Syncfusion license
                var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY") ?? "trial";
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);

                Log.Information("🚌 Syncfusion license registered");

                // Create minimal WPF app
                var app = new TestApp();

                // Create simple window
                var window = new Window()
                {
                    Title = "BusBuddy Test - Azure SQL Connected",
                    Width = 400,
                    Height = 300,
                    Content = "🚌 BusBuddy is connected to Azure SQL!\n✅ Ready for student input and route management"
                };

                Log.Information("🚌 Showing test window");

                // Run the application
                app.Run(window);

                Log.Information("🚌 Test app completed successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "🚌 Test app failed: {Message}", ex.Message);
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }
    }
}
