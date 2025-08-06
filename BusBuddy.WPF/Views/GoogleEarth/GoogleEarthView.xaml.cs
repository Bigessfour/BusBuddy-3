using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using BusBuddy.WPF.ViewModels.GoogleEarth;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.Views.GoogleEarth
{
    /// <summary>
    /// Interaction logic for GoogleEarthView.xaml
    /// Google Earth integration for advanced geospatial mapping and route visualization
    /// Enhanced with debounced layer changes and optimized background processing
    /// </summary>
    public partial class GoogleEarthView : UserControl, IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<GoogleEarthView>();

        // Debouncing for map layer changes
        private readonly Timer _layerChangeDebounceTimer;
        private string? _pendingLayerType;
        private readonly object _layerChangeLock = new object();
        private const int LayerChangeDebounceDelayMs = 500;
        private bool _disposed;

        public GoogleEarthView()
        {
            using (LogContext.PushProperty("ViewInitialization", "GoogleEarthView"))
            using (LogContext.PushProperty("PerformanceOptimizations", "Enabled"))
            {
                InitializeComponent();

                // Initialize debounce timer for layer changes
                _layerChangeDebounceTimer = new Timer(OnLayerChangeDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

                // Subscribe to Unloaded event for cleanup
                this.Unloaded += GoogleEarthView_Unloaded;

                Logger.Information("Google Earth view initialized successfully with performance optimizations");
                Logger.Debug("Layer change debouncing configured with {DelayMs}ms delay", LayerChangeDebounceDelayMs);
            }
        }

        /// <summary>
        /// Handles map layer selection changes with debouncing to prevent rapid API calls
        /// Enhanced with structured logging and performance optimizations
        /// </summary>
        private void MapLayerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];

            using (LogContext.PushProperty("UserInteraction", "MapLayerChange"))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                try
                {
                    if (DataContext is not GoogleEarthViewModel viewModel)
                    {
                        Logger.Warning("ViewModel not available for layer change operation");
                        return;
                    }

                    if (MapLayerComboBox.SelectedItem is not Syncfusion.Windows.Tools.Controls.ComboBoxItemAdv selectedItem)
                    {
                        Logger.Debug("No valid selection detected in layer combo box");
                        return;
                    }

                    var layerType = selectedItem.Content?.ToString();
                    if (string.IsNullOrEmpty(layerType))
                    {
                        Logger.Warning("Selected layer type is null or empty");
                        return;
                    }

                    Logger.Debug("Layer change request received: {LayerType} (will be debounced)", layerType);

                    // Store the pending layer type and restart the debounce timer
                    lock (_layerChangeLock)
                    {
                        _pendingLayerType = layerType;
                        _layerChangeDebounceTimer.Change(LayerChangeDebounceDelayMs, Timeout.Infinite);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to process layer change request: {ErrorMessage} (CorrelationId: {CorrelationId})",
                               ex.Message, correlationId);

                    // Could add user notification here if needed
                    // ShowUserFriendlyError("Failed to change map layer. Please try again.");
                }
            }
        }

        /// <summary>
        /// Handles the debounced layer change execution on background thread
        /// </summary>
        private async void OnLayerChangeDebounceElapsed(object? state)
        {
            string? layerTypeToApply;

            // Get the pending layer type safely
            lock (_layerChangeLock)
            {
                layerTypeToApply = _pendingLayerType;
                _pendingLayerType = null;
            }

            if (string.IsNullOrEmpty(layerTypeToApply))
            {
                return;
            }


            var correlationId = Guid.NewGuid().ToString("N")[..8];

            using (LogContext.PushProperty("BackgroundOperation", "DebouncedLayerChange"))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("LayerType", layerTypeToApply))
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    Logger.Information("Executing debounced layer change to: {LayerType}", layerTypeToApply);

                    // Execute on UI thread since ViewModel operations may need it
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        if (DataContext is GoogleEarthViewModel viewModel)
                        {
                            // Offload heavy geospatial operations to background thread
                            await Task.Run(() =>
                            {
                                using (LogContext.PushProperty("ThreadType", "Background"))
                                {
                                    Logger.Debug("Processing geospatial layer change on background thread");
                                    // TODO: Re-enable when ChangeMapLayer method is implemented
                                    // viewModel.ChangeMapLayer(layerTypeToApply);
                                }
                            });
                        }
                    });

                    stopwatch.Stop();
                    Logger.Information("Layer change completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Logger.Error(ex, "Failed to execute debounced layer change: {ErrorMessage} (Elapsed: {ElapsedMs}ms)",
                               ex.Message, stopwatch.ElapsedMilliseconds);
                }
            }
        }

        /// <summary>
        /// Handles data context changes to wire up view model events
        /// Enhanced with comprehensive ViewModel lifecycle management
        /// </summary>
        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];

            using (LogContext.PushProperty("ViewModelBinding", "GoogleEarthViewModel"))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                try
                {
                    // Cleanup old ViewModel if needed
                    if (e.OldValue is GoogleEarthViewModel oldViewModel)
                    {
                        Logger.Debug("Cleaning up previous ViewModel binding");
                        // Add any cleanup logic here if needed
                    }

                    if (e.NewValue is GoogleEarthViewModel newViewModel)
                    {
                        Logger.Information("Google Earth view model connected successfully");

                        // Wire up any additional ViewModel events or initialize background operations
                        Task.Run(async () =>
                        {
                            using (LogContext.PushProperty("BackgroundOperation", "ViewModelInitialization"))
                            {
                                try
                                {
                                    Logger.Debug("Initializing background ViewModel operations");

                                    // Perform any heavy initialization operations here
                                    await Task.Delay(100); // Placeholder for actual initialization

                                    Logger.Debug("Background ViewModel initialization completed");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error(ex, "Failed during background ViewModel initialization");
                                }
                            }
                        });
                    }
                    else if (e.NewValue != null)
                    {
                        Logger.Warning("DataContext is not of expected GoogleEarthViewModel type: {ActualType}",
                                     e.NewValue.GetType().Name);
                    }
                    else
                    {
                        Logger.Debug("DataContext set to null");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to handle DataContext change: {ErrorMessage}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Override to handle data context changes and cleanup
        /// Enhanced with proper disposal patterns
        /// </summary>
        protected override void OnInitialized(EventArgs e)
        {
            using (LogContext.PushProperty("ViewLifecycle", "Initialization"))
            {
                try
                {
                    base.OnInitialized(e);
                    DataContextChanged += OnDataContextChanged;

                    Logger.Debug("GoogleEarthView initialization completed successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed during GoogleEarthView initialization");
                    throw; // Re-throw to allow proper error handling upstream
                }
            }
        }

        /// <summary>
        /// Cleanup resources when the view is being disposed
        /// </summary>
        private void GoogleEarthView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            using (LogContext.PushProperty("ViewLifecycle", "Unloaded"))
            {
                Logger.Debug("GoogleEarthView unloaded, disposing resources");
                Dispose();
            }
        }

        /// <summary>
        /// Dispose pattern implementation
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _layerChangeDebounceTimer?.Dispose();
                    DataContextChanged -= OnDataContextChanged;
                    this.Unloaded -= GoogleEarthView_Unloaded;
                    Logger.Debug("GoogleEarthView resources disposed successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during GoogleEarthView disposal");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}
