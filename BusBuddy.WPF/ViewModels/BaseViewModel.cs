using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing common functionality
    /// </summary>
    public abstract partial class BaseViewModel : ObservableObject
    {
        /// <summary>
        /// Static logger instance for this class
        /// </summary>
        protected static readonly ILogger Logger = Log.ForContext<BaseViewModel>();

        /// <summary>
        /// Indicates if the ViewModel is currently performing a loading operation
        /// </summary>
        [ObservableProperty]
        private bool isLoading;

        /// <summary>
        /// Status message for user feedback
        /// </summary>
        [ObservableProperty]
        private string statusMessage = string.Empty;
    }
}
