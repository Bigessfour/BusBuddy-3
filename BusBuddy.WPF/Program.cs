using System;
using System.Threading;
using System.Windows;

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
            // Ensure STA apartment state is set
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

            var app = new App();
            app.Run();
        }
    }
}
