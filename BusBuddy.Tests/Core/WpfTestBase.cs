using System;
using System.Threading;
using System.Windows.Threading;
using NUnit.Framework;
using Syncfusion.Licensing;

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Base class for WPF UI tests that provides STA threading and Syncfusion licensing
    /// </summary>
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public abstract class WpfTestBase : TestBase
    {
        protected Dispatcher Dispatcher { get; private set; } = null!;

        [OneTimeSetUp]
        public virtual void OneTimeWpfSetUp()
        {
            // Register Syncfusion license for tests
            var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrEmpty(licenseKey))
            {
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            }

            // Create WPF dispatcher for UI thread operations
            if (Dispatcher.CurrentDispatcher == null)
            {
                Dispatcher = Dispatcher.CurrentDispatcher;
            }
            else
            {
                Dispatcher = Dispatcher.CurrentDispatcher;
            }
        }

        [SetUp]
        public virtual void WpfSetUp()
        {
            // Ensure we're on the UI thread for WPF operations
            if (Dispatcher.CheckAccess())
            {
                // Already on UI thread
                return;
            }

            // If not on UI thread, dispatch the setup
            Dispatcher.Invoke(() => { });
        }

        [TearDown]
        public virtual void WpfTearDown()
        {
            // Clean up any WPF resources
            Dispatcher.Invoke(() => { });
        }

        /// <summary>
        /// Executes an action on the UI thread
        /// </summary>
        protected void ExecuteOnUIThread(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result
        /// </summary>
        protected T ExecuteOnUIThread<T>(Func<T> func)
        {
            if (Dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                return Dispatcher.Invoke(func);
            }
        }
    }
}
