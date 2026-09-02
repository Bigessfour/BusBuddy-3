using System;
using System.Threading;
using System.Windows;

namespace BusBuddy.WPF
{
    /// <summary>
    /// Program entry point with STAThread attribute to fix WPF threading issues.
    /// Syncfusion license registration happens in App() constructor per Syncfusion WPF guidance.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BusBuddy.Core.Utilities.EntityFrameworkPostgresExtensions.ConfigureNpgsqlAppContext();
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

            var app = new App();
            app.Run();
        }
    }
}
