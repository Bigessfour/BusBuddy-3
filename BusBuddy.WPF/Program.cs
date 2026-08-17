using System;
using System.IO;
using System.Threading;
using System.Windows;
using Syncfusion.Licensing;

namespace BusBuddy.WPF
{
    /// <summary>
    /// Program entry point with STAThread attribute to fix WPF threading issues
    /// Required for proper WPF and Syncfusion component initialization
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Application entry point with STA threading for WPF
        /// </summary>
        /// <param name="args">Command line arguments</param>
        [STAThread]
        public static void Main(string[] args)
        {
            // License must be registered before App/XAML loads any Syncfusion types.
            RegisterSyncfusionLicenseEarly();

            // Ensure STA apartment state is set
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

            var app = new App();
            app.Run();
        }

        private static void RegisterSyncfusionLicenseEarly()
        {
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (string.IsNullOrWhiteSpace(key))
            {
                foreach (var path in new[]
                {
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "keys", "SYNCFUSION_LICENSE_KEY.txt")),
                    Path.Combine(Directory.GetCurrentDirectory(), "keys", "SYNCFUSION_LICENSE_KEY.txt"),
                    @"C:\dev\BusBuddy-3\keys\SYNCFUSION_LICENSE_KEY.txt"
                })
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            key = File.ReadAllText(path).Trim();
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                Environment.SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", key);
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Fall through to env-only registration
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(key) && key.Length >= 20)
            {
                SyncfusionLicenseProvider.RegisterLicense(key);
            }
        }
    }
}
