using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing common functionality
    /// </summary>
    public abstract class BaseViewModel : ObservableObject
    {
        #region Fields
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        #endregion

        #region Properties
        /// <summary>
        /// Static logger instance for this class
        /// </summary>
        protected static readonly ILogger Logger = Log.ForContext<BaseViewModel>();

        /// <summary>
        /// Indicates if the ViewModel is currently performing a loading operation
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Status message for user feedback
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        #endregion
    }
}
