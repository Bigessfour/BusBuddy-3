using System.ComponentModel;
using System.Runtime.CompilerServices;
using Serilog;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// MVP Base ViewModel without external dependencies
    /// Temporary implementation for Monday demo
    /// </summary>
    public abstract class BaseViewModelMvp : BaseViewModel
    {
        private bool _isLoading;
        public new bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Logs information messages with the ViewModel context
        /// </summary>
        protected void LogInformation(string message, params object[] args)
        {
            Log.Information($"[{GetType().Name}] {message}", args);
        }

        /// <summary>
        /// Logs error messages with the ViewModel context
        /// </summary>
        protected void LogError(string message, params object[] args)
        {
            Log.Error($"[{GetType().Name}] {message}", args);
        }

        /// <summary>
        /// Logs warning messages with the ViewModel context
        /// </summary>
        protected void LogWarning(string message, params object[] args)
        {
            Log.Warning($"[{GetType().Name}] {message}", args);
        }
    }
}
