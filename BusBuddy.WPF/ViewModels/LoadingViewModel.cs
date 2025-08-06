using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.ViewModels
{
    public class LoadingViewModel : BaseViewModel
    {
        private string _status = "Initializing...";
        private string _progressMessage = "Starting Bus Buddy application...";
        private int _progressPercentage;
        private bool _isIndeterminate = true;
        private bool _isComplete;

        public LoadingViewModel()
        {
            using (LogContext.PushProperty("ViewModelType", nameof(LoadingViewModel)))
            using (LogContext.PushProperty("OperationType", "Construction"))
            {
                Logger.Information("LoadingViewModel created with enhanced progress tracking");
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();

                    using (LogContext.PushProperty("ViewModelType", nameof(LoadingViewModel)))
                    using (LogContext.PushProperty("OperationType", "StatusChange"))
                    {
                        Logger.Debug("Loading status changed to: {Status}", value);
                    }
                }
            }
        }

        /// <summary>
        /// Detailed progress message for real-time updates
        /// </summary>
        public string ProgressMessage
        {
            get => _progressMessage;
            set
            {
                if (_progressMessage != value)
                {
                    _progressMessage = value;
                    OnPropertyChanged();

                    using (LogContext.PushProperty("ViewModelType", nameof(LoadingViewModel)))
                    using (LogContext.PushProperty("OperationType", "ProgressMessageChange"))
                    {
                        Logger.Debug("Progress message updated: {ProgressMessage}", value);
                    }
                }
            }
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                if (_progressPercentage != value)
                {
                    _progressPercentage = value;
                    OnPropertyChanged();

                    using (LogContext.PushProperty("ViewModelType", nameof(LoadingViewModel)))
                    using (LogContext.PushProperty("OperationType", "ProgressChange"))
                    {
                        Logger.Debug("Progress changed to: {Progress}%", value);

                        // Update IsComplete when progress reaches 100%
                        if (value >= 100 && !_isComplete)
                        {
                            _isComplete = true;
                            OnPropertyChanged(nameof(IsComplete));
                            Logger.Information("✅ Application initialization completed - ready for use");
                        }
                    }
                }
            }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                if (_isIndeterminate != value)
                {
                    _isIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Indicates if the initialization process is complete and UI is ready
        /// </summary>
        public bool IsComplete
        {
            get => _isComplete;
            private set
            {
                if (_isComplete != value)
                {
                    _isComplete = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Event fired when initialization is complete
        /// </summary>
        public event EventHandler? InitializationCompleted;

        /// <summary>
        /// Reset the loading state for a new initialization sequence
        /// </summary>
        public void Reset()
        {
            using (LogContext.PushProperty("ViewModelType", nameof(LoadingViewModel)))
            using (LogContext.PushProperty("OperationType", "Reset"))
            {
                ProgressPercentage = 0;
                Status = "Initializing...";
                ProgressMessage = "Starting Bus Buddy application...";
                IsIndeterminate = true;
                _isComplete = false;
                OnPropertyChanged(nameof(IsComplete));
                Logger.Information("🔄 LoadingViewModel reset for new initialization sequence");
            }
        }

        /// <summary>
        /// Mark initialization as complete and fire completion event
        /// </summary>
        public void CompleteInitialization()
        {
            ProgressPercentage = 100;
            Status = "Application ready!";
            ProgressMessage = "Bus Buddy is ready for use";
            IsIndeterminate = false;
            InitializationCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Update both status and progress message with percentage
        /// </summary>
        public void UpdateProgress(string status, string progressMessage, int percentage)
        {
            Status = status;
            ProgressMessage = progressMessage;
            ProgressPercentage = percentage;
            IsIndeterminate = false;
        }

        /// <summary>
        /// Set indeterminate progress with detailed messages
        /// </summary>
        public void SetIndeterminateProgress(string status, string progressMessage)
        {
            Status = status;
            ProgressMessage = progressMessage;
            IsIndeterminate = true;
        }
    }
}
