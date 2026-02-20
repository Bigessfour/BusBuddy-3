using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Serilog;
using BusBuddy.Tests.Core;

namespace BusBuddy.Tests.ViewModels
{
    /// <summary>
    /// Base class for ViewModel unit tests with proper MVVM patterns
    /// </summary>
    [TestFixture]
    public abstract class ViewModelTestBase : TestBase
    {
        protected Mock<Microsoft.Extensions.Logging.ILogger> LoggerMock { get; private set; } = null!;

        [SetUp]
        public virtual void ViewModelSetUp()
        {
            // Setup logger mock for ViewModel testing
            LoggerMock = new Mock<Microsoft.Extensions.Logging.ILogger>();
            LoggerMock.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>()))
                     .Returns(true);
        }

        /// <summary>
        /// Verifies that property changed events are raised correctly
        /// </summary>
        protected async Task VerifyPropertyChangedAsync<TViewModel>(
            TViewModel viewModel,
            string propertyName,
            Func<TViewModel, Task> action,
            int expectedChangeCount = 1) where TViewModel : ObservableObject
        {
            var changeCount = 0;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == propertyName)
                    changeCount++;
            };

            await action(viewModel);

            Assert.That(changeCount, Is.EqualTo(expectedChangeCount),
                $"Property '{propertyName}' should have changed {expectedChangeCount} time(s)");
        }

        /// <summary>
        /// Verifies that a command can execute
        /// </summary>
        protected void VerifyCommandCanExecute(IAsyncRelayCommand command, bool expectedCanExecute)
        {
            Assert.That(command.CanExecute(null), Is.EqualTo(expectedCanExecute),
                $"Command should {(expectedCanExecute ? "be able to execute" : "not be able to execute")}");
        }

        /// <summary>
        /// Verifies that a command executes successfully
        /// </summary>
        protected async Task VerifyCommandExecutionAsync(IAsyncRelayCommand command)
        {
            await command.ExecuteAsync(null);
            // Command executed without throwing exception
        }

        /// <summary>
        /// Creates a mock logger for testing
        /// </summary>
        protected Mock<ILogger<T>> CreateLoggerMock<T>()
        {
            var mock = new Mock<ILogger<T>>();
            mock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
                .Returns(true);
            return mock;
        }
    }
}
