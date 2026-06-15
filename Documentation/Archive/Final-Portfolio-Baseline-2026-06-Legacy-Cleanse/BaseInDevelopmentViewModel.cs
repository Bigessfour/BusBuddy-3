using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.ViewModels
{
    /// <summary>
    /// Base class for view models that are in development status
    /// This provides common properties for showing placeholder content
    /// </summary>
    public abstract class BaseInDevelopmentViewModel : BaseViewModel
    {
        private bool _isInDevelopment = true;

        protected BaseInDevelopmentViewModel()
        {
            using (LogContext.PushProperty("ViewModelType", GetType().Name))
            using (LogContext.PushProperty("OperationType", "Construction"))
            {
                Logger.Information("{ViewModelType} initialized in development mode", GetType().Name);
            }
        }

        /// <summary>
        /// Indicates whether this module is in development mode
        /// Controls visibility of placeholder overlay in views
        /// </summary>
        public bool IsInDevelopment
        {
            get => _isInDevelopment;
            set => SetProperty(ref _isInDevelopment, value);
        }
    }
}
