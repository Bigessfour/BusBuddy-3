using System;
using System.Windows;
using BusBuddy.WPF.Views.Main;

namespace BusBuddy.WPF.Testing
{
    public static class StartupTest
    {
        public static void TestMainWindowCreation()
        {
            try
            {
                Console.WriteLine("Testing MainWindow creation...");
                var window = new MainWindow();
                Console.WriteLine("✅ MainWindow created successfully");
                window.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MainWindow creation failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}
