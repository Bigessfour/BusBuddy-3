using System;
using System.Threading;
using System.Windows;

namespace BusBuddy.WPF
{
    /// <summary>
    /// Modern .NET 9 WPF application entry point
    /// Ensures STA threading required for WPF UI components
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Application entry point with proper WPF threading
        /// </summary>
        /// <param name="args">Command line arguments (currently unused)</param>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                // Ensure STA apartment state for WPF
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

                // Create and run the WPF application
                var app = new App();
                app.Run();
            }
            catch (Exception ex)
            {
                // Fallback error handling if app fails to start
                System.Diagnostics.Debug.WriteLine($"Critical startup error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
