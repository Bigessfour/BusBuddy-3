// MainWindow code-behind trimmed for MVP; extensive troubleshooting notes moved to GROK-README.md.
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Syncfusion.UI.Xaml.Grid;
using BusBuddy.WPF.ViewModels;
using BusBuddy.WPF.Views.Dashboard;
using BusBuddy.WPF.ViewModels.Route; // Needed for RouteManagementViewModel reference
using BusBuddy.WPF.Views.Student;
using BusBuddy.WPF.Views.Bus;
using BusBuddy.WPF.Views.Driver;
using BusBuddy.WPF.Views.Analytics;
using BusBuddy.WPF.Views.Route;
using BusBuddy.WPF.Views.Settings;
using BusBuddy.WPF.Views.Vehicle;
using BusBuddy.WPF.Views.Reports;
using BusBuddy.Core.Services;
using BusBuddy.Core.Data;
using Syncfusion.SfSkinManager;
using Serilog;
using Syncfusion.Windows.Tools.Controls; // DockingManager API per Syncfusion docs
using Syncfusion.Windows.Shared; // ChromelessWindow API per Syncfusion docs
using BusBuddy.WPF.Services.Navigation; // navigation service for DockingManager documents

namespace BusBuddy.WPF.Views.Main
{
    /// <summary>
    /// BusBuddy MainWindow - MVP Implementation with Syncfusion ChromelessWindow and DockingManager
    /// Professional layout with validated Syncfusion patterns using ChromelessWindow for modern UI
    /// </summary>
    public partial class MainWindow : ChromelessWindow
    {
    // Explicit reference placeholder to satisfy analyzer if generated partial field not yet recognized
    // At runtime, the XAML-generated field MainDockingManager will be used.
    private DockingManager? _designTimeDockingManagerAccessor => this.FindName("MainDockingManager") as DockingManager;
        private static readonly ILogger Logger = Log.ForContext<MainWindow>();
        private static readonly int DockActivatedWidthBump = 120; // consolidated width bump constant
        private readonly Guid _windowInstanceId = Guid.NewGuid(); // correlation id for structured logs
        private BusBuddy.WPF.ViewModels.MainWindowViewModel? _viewModel;
        private INavigationService? _navigationService; // injected or initialized after DockingManager ready

    // Generated fields (StudentsGrid, MainDockingManager, etc.) come from XAML partial class after InitializeComponent.

    // DI-friendly constructor to ensure DataContext is set before initialization
        public MainWindow(BusBuddy.WPF.ViewModels.MainWindowViewModel viewModel) : this()
        {
            try
            {
                Logger.Debug("DI constructor: applying provided MainWindowViewModel");
                _viewModel = viewModel;
                this.DataContext = _viewModel;
                Logger.Information("MainWindow DataContext set from DI constructor");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to set DataContext from DI constructor");
            }
        }

        public MainWindow()
        {
            Logger.Debug("MainWindow constructor starting");
            try
            {
                Logger.Debug("Calling InitializeComponent to build visual tree");
                // Restore normal WPF initialization so x:Name fields (StudentsGrid, MainDockingManager, etc.) are generated
                // Explicit 'this.' to help certain analyzers/linkers detect generated partial method
                this.InitializeComponent();

                Logger.Debug("Applying Syncfusion theme");
                ApplySyncfusionTheme();

                Logger.Debug("Initializing MainWindow components and DataContext");
                InitializeMainWindow();

                // Wire up lifecycle events once visual tree is ready
                try
                {
                    this.Loaded += MainWindow_Loaded;
                    this.Closing += MainWindow_Closing;
                }
                catch (Exception hookEx)
                {
                    Logger.Warning(hookEx, "Failed wiring lifecycle events");
                }

                // Global button click diagnostics: capture all button clicks in this window
                try
                {
                    AddHandler(ButtonBase.ClickEvent, new System.Windows.RoutedEventHandler(OnAnyButtonClick), true);
                    AddHandler(Selector.SelectionChangedEvent, new System.Windows.Controls.SelectionChangedEventHandler(OnAnySelectionChanged), true);
                    AddHandler(TextBoxBase.TextChangedEvent, new System.Windows.Controls.TextChangedEventHandler(OnAnyTextChanged), true);
                    AddHandler(System.Windows.Controls.Validation.ErrorEvent, new System.EventHandler<System.Windows.Controls.ValidationErrorEventArgs>(OnValidationError), true);
                    Logger.Information("Global button click diagnostics handler attached");
                }
                catch (Exception btnEx)
                {
                    Logger.Warning(btnEx, "Failed attaching global button click diagnostics");
                }

                // Attach Syncfusion SfDataGrid error hooks for runtime diagnostics
                Logger.Debug("Attaching Syncfusion event hooks");
                AttachSyncfusionEventHooks();

                // Wire DockingManager activation events for dynamic sizing
                try
                {
                    if (MainDockingManager != null)
                    {
                        MainDockingManager.WindowActivated += DockingManager_WindowActivated; // https://help.syncfusion.com/cr/wpf/Syncfusion.Windows.Tools.Controls.DockingManager.html#events
                        MainDockingManager.WindowDeactivated += DockingManager_WindowDeactivated;
                        Logger.Information("DockingManager activation events wired (field access)");
                    }
                    else
                    {
                        Logger.Warning("MainDockingManager field is null after InitializeComponent");
                    }
                }
                catch (Exception evtEx)
                {
                    Logger.Warning(evtEx, "Failed wiring DockingManager activation events");
                }

                Logger.Information("MainWindow initialized successfully with Syncfusion DockingManager");
                Logger.Debug("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize MainWindow");
                Logger.Debug("Creating fallback layout due to initialization failure");
                CreateFallbackLayout();
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                InitializeProgrammaticDockLayout(); // step 2
                RegisterNavigationPanes(); // step 5
                // Delay audit slightly to ensure visual tree fully ready
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { AuditButtonsAccessibility(); } catch (Exception ex2) { Logger.Warning(ex2, "MainWindow: post-load audit failed"); }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainWindow_Loaded diagnostics failed");
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                PersistDockLayout();
                RemoveHandler(ButtonBase.ClickEvent, new System.Windows.RoutedEventHandler(OnAnyButtonClick));
                RemoveHandler(Selector.SelectionChangedEvent, new System.Windows.Controls.SelectionChangedEventHandler(OnAnySelectionChanged));
                RemoveHandler(TextBoxBase.TextChangedEvent, new System.Windows.Controls.TextChangedEventHandler(OnAnyTextChanged));
                RemoveHandler(System.Windows.Controls.Validation.ErrorEvent, new System.EventHandler<System.Windows.Controls.ValidationErrorEventArgs>(OnValidationError));
            }
            catch { }
        }

        /// <summary>
        /// Attach event hooks to Syncfusion controls for runtime error capture
        /// </summary>
        private void AttachSyncfusionEventHooks()
        {
            try
            {
                // ===================================================================
                // SYNCFUSION EVENT HOOKS - DISABLED FOR MVP STABILITY
                // ===================================================================

                // For MVP stability, basic functionality is prioritized over advanced events
                Logger.Information("Syncfusion event hooks ready (basic functionality enabled)");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Syncfusion event hook preparation completed with warnings");
            }
        }

        /// <summary>
        /// Syncfusion SfDataGrid cell error handler for runtime diagnostics
        /// This method is ready for use once XAML controls are properly defined
        /// </summary>
        private void SfDataGrid_QueryCellInfo(object sender, object e)
        {
            try
            {
                // Generic event handler that will work with any Syncfusion grid event
                // Once proper using Syncfusion.UI.Xaml.Grid; is added, change parameter to:
                // private void SfDataGrid_QueryCellInfo(object sender, GridQueryCellInfoEventArgs e)

                // Cell processing logic would go here
                // For now, this is just an error capture wrapper
            }
            catch (Exception ex)
            {
                var gridName = (sender as FrameworkElement)?.Name ?? "UnknownGrid";
                Logger.Error(ex, "SfDataGrid cell error Grid={Grid} WindowId={WindowId}", gridName, _windowInstanceId);
            }
        }

        private void ApplySyncfusionTheme()
        {
            Logger.Debug("ApplySyncfusionTheme method started");
            try
            {
                Logger.Debug("Configuring Syncfusion SfSkinManager global settings");
                // Apply FluentDark theme with FluentLight fallback
                // Based on SYNCFUSION_API_REFERENCE.md validated patterns
                SfSkinManager.ApplyStylesOnApplication = true;
                SfSkinManager.ApplyThemeAsDefaultStyle = true;

                Logger.Debug("Creating FluentDark theme instance");
                using var fluentDarkTheme = new Theme("FluentDark");
                Logger.Debug("Applying FluentDark theme to MainWindow");
                SfSkinManager.SetTheme(this, fluentDarkTheme);

                Logger.Information("Applied FluentDark theme successfully");
                Logger.Debug("ApplySyncfusionTheme completed with FluentDark");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to apply FluentDark theme, trying FluentLight fallback");

                try
                {
                    Logger.Debug("Attempting FluentLight fallback theme");
                    using var fluentLightTheme = new Theme("FluentLight");
                    SfSkinManager.SetTheme(this, fluentLightTheme);
                    Logger.Information("Applied FluentLight fallback theme successfully");
                    Logger.Debug("ApplySyncfusionTheme completed with FluentLight fallback");
                }
                catch (Exception fallbackEx)
                {
                    Logger.Error(fallbackEx, "Failed to apply any Syncfusion theme");
                    Logger.Debug("ApplySyncfusionTheme failed completely, continuing without theme");
                }
            }
        }

        private void InitializeMainWindow()
        {
            Logger.Debug("InitializeMainWindow method started");

            Logger.Debug("Ensuring robust DataContext management");
            // Create and set ViewModel if not already present
            if (this.DataContext == null || this.DataContext is not BusBuddy.WPF.ViewModels.MainWindowViewModel)
            {
                Logger.Debug("Creating new MainWindowViewModel instance");
                _viewModel = new BusBuddy.WPF.ViewModels.MainWindowViewModel();
                this.DataContext = _viewModel;
                Logger.Information("MainWindow DataContext initialized with new ViewModel");
            }
            else
            {
                Logger.Debug("Existing MainWindowViewModel found, preserving it");
                _viewModel = (BusBuddy.WPF.ViewModels.MainWindowViewModel)this.DataContext;
                Logger.Information("MainWindow DataContext preserved from DI");
            }

            // Ensure DataContext persistence
            this.DataContextChanged += MainWindow_DataContextChanged;

            Logger.Debug("InitializeMainWindow method completed");
        }

        // Generate eligibility route PDF directly from MainWindow without needing GoogleEarthView visible.
        private async void EligibilityPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (Serilog.Context.LogContext.PushProperty("UIAction", "EligibilityPdf"))
                {
                    Logger.Information("Eligibility PDF button clicked (MainWindow)");
                    var sp = App.ServiceProvider;
                    var vm = sp?.GetService<BusBuddy.WPF.ViewModels.GoogleEarth.GoogleEarthViewModel>();
                    if (vm == null)
                    {
                        Logger.Warning("GoogleEarthViewModel not resolved for eligibility PDF generation");
                        System.Windows.MessageBox.Show("Map ViewModel not available (GoogleEarthViewModel)", "Eligibility PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    await vm.GenerateEligibilityRoutePdfAndSaveAsync();
                    System.Windows.MessageBox.Show("Eligibility PDF generated. Check PdfReports folder.", "Eligibility PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Eligibility PDF generation failed from MainWindow");
                System.Windows.MessageBox.Show($"Eligibility PDF error: {ex.Message}", "Eligibility PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Print the last generated eligibility PDF via shell print verb.
        private void PrintEligibilityPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sp = App.ServiceProvider;
                var vm = sp?.GetService<BusBuddy.WPF.ViewModels.GoogleEarth.GoogleEarthViewModel>();
                var path = vm?.LastGeneratedEligibilityPdfPath;
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                {
                    System.Windows.MessageBox.Show("No previously generated eligibility PDF found.", "Print Eligibility PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                using (Serilog.Context.LogContext.PushProperty("UIAction", "PrintEligibilityPdf"))
                {
                    Logger.Information("Printing eligibility PDF {Path}", path);
                }
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    Verb = "print",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to print eligibility PDF");
                System.Windows.MessageBox.Show($"Print failed: {ex.Message}", "Print Eligibility PDF", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Global button click logger for MainWindow
        private void OnAnyButtonClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is DependencyObject src)
                {
                    var fe = src as FrameworkElement;
                    string name = fe?.Name ?? "(unnamed)";
                    string type = src.GetType().Name;
                    string? label = null;
                    if (src is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                    {
                        label = badv.Content?.ToString();
                    }
                    else if (src is Button b)
                    {
                        label = b.Content?.ToString();
                    }
                    Logger.Information("MainWindow ButtonClick: Type={Type} Name={Name} Label={Label}", type, name, label);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainWindow: OnAnyButtonClick logging failed");
            }
        }

        // Global validation diagnostics
        private void OnValidationError(object? sender, ValidationErrorEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");
                Logger.Warning("MainWindow Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainWindow: validation logging failed");
            }
        }

        // Global selection change diagnostics (mirrors pattern used in other views)
        private void OnAnySelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is DependencyObject src)
                {
                    var fe = src as FrameworkElement;
                    string name = fe?.Name ?? "(unnamed)";
                    string type = src.GetType().Name;
                    Logger.Information("MainWindow SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainWindow: selection change logging failed");
            }
        }

        // Global text change diagnostics
        private void OnAnyTextChanged(object? sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is DependencyObject src)
                {
                    var fe = src as FrameworkElement;
                    string name = fe?.Name ?? "(unnamed)";
                    string type = src.GetType().Name;
                    Logger.Information("MainWindow TextChanged: Type={Type} Name={Name}", type, name);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainWindow: text change logging failed");
            }
        }

        // Centralized theme application across open windows using SyncfusionThemeManager helper
        private void ApplyThemeGlobally(string themeName)
        {
            try
            {
                void ApplyTo(DependencyObject d)
                {
                    try
                    {
                        using var theme = new Syncfusion.SfSkinManager.Theme(themeName);
                        SfSkinManager.SetTheme(d, theme);
                    }
                    catch { }
                }
                ApplyTo(this);
                if (Application.Current != null)
                {
                    foreach (Window w in Application.Current.Windows)
                    {
                        ApplyTo(w);
                    }
                }
                Logger.Information("Applied theme {Theme} globally", themeName);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed applying theme {Theme} globally", themeName);
            }
        }

        // Accessibility / command readiness audit for Buttons & ButtonAdv controls
        private void AuditButtonsAccessibility()
        {
            try
            {
                int total = 0, adv = 0, missingLabel = 0, missingAuto = 0, noCmd = 0;
                foreach (var d in Traverse(this))
                {
                    if (d is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                    {
                        total++; adv++;
                        var label = badv.Label; var autoName = System.Windows.Automation.AutomationProperties.GetName(badv);
                        bool hasCmd = badv.Command != null; if (!hasCmd) noCmd++;
                        if (string.IsNullOrWhiteSpace(label)) missingLabel++;
                        if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                    }
                    else if (d is Button btn)
                    {
                        total++;
                        var content = btn.Content?.ToString(); var autoName = System.Windows.Automation.AutomationProperties.GetName(btn);
                        bool hasCmd = btn.Command != null; if (!hasCmd) noCmd++;
                        if (string.IsNullOrWhiteSpace(content)) missingLabel++;
                        if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                    }
                }
                Logger.Information("MainWindow Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}", total, adv, missingLabel, missingAuto, noCmd);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "MainWindow: accessibility audit failed");
            }
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Traverse(DependencyObject root)
        {
            if (root == null) yield break;
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child == null) continue;
                yield return child;
                foreach (var g in Traverse(child)) yield return g;
            }
        }

        // Store original widths to restore on deactivation
        // Removed dynamic width bump logic (Step 2 refactor) — static widths defined in XAML
        private const string LayoutPersistenceFile = "DockLayout.xml"; // serialization filename

        // Based on Syncfusion DockingManager events and attached properties:
        // - WindowActivated / WindowDeactivated events are documented in API reference.
        // - DesiredWidthInDockedMode is an attached property used for docked panes.
        // Docs: https://help.syncfusion.com/cr/wpf/Syncfusion.Windows.Tools.Controls.DockingManager.html
        private void DockingManager_WindowActivated(object? sender, RoutedEventArgs e) { /* width bump removed */ }
        private void DockingManager_WindowDeactivated(object? sender, RoutedEventArgs e) { /* width bump removed */ }

        private void InitializeProgrammaticDockLayout()
        {
            if (MainDockingManager == null) return;
            try
            {
                // Attempt layout load first
                if (System.IO.File.Exists(LayoutPersistenceFile))
                {
                    using var reader = new System.IO.StreamReader(LayoutPersistenceFile);
                    MainDockingManager.LoadDockState(reader);
                    Logger.Information("Restored dock layout from {File}", LayoutPersistenceFile);
                    return;
                }

                // Default layout is already declared in XAML (StudentsPane left, RoutesPane right, documents center)
                Logger.Information("Using default XAML dock layout (no persisted layout found)");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to initialize programmatic dock layout (fallback to XAML state)");
            }
        }

        private void PersistDockLayout()
        {
            if (MainDockingManager == null) return;
            try
            {
                var settings = new System.Xml.XmlWriterSettings { Indent = true, OmitXmlDeclaration = false };
                using var writer = System.Xml.XmlWriter.Create(LayoutPersistenceFile, settings);
                MainDockingManager.SaveDockState(writer);
                Logger.Information("Persisted dock layout to {File}", LayoutPersistenceFile);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed persisting dock layout");
            }
        }

    // Duplicate MainWindow_Loaded and MainWindow_Closing removed (original definitions kept earlier in file)

        private void RegisterNavigationPanes()
        {
            if (_navigationService == null) return;
            try
            {
                _navigationService.Register(new PaneDescriptor { Key = "dashboard", Header = "📊 Dashboard", Factory = () => new BusBuddy.WPF.Views.Dashboard.DashboardView() });
                _navigationService.Register(new PaneDescriptor { Key = "students", Header = "📚 Students", Factory = () => new BusBuddy.WPF.Views.Student.StudentsView() });
                _navigationService.Register(new PaneDescriptor { Key = "buses", Header = "🚐 Buses", Factory = () => new VehicleManagementView() });
                _navigationService.Register(new PaneDescriptor { Key = "drivers", Header = "👨‍✈️ Drivers", Factory = () => new DriversView() });
                _navigationService.Register(new PaneDescriptor { Key = "routes", Header = "🚌 Routes", Factory = () => new RouteManagementView() });
                _navigationService.Register(new PaneDescriptor { Key = "map", Header = "🌍 Map", Factory = () => new BusBuddy.WPF.Views.GoogleEarth.GoogleEarthView() });
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed registering navigation panes");
            }
        }

        /// <summary>
        /// Handle DataContext changes to prevent loss of button functionality
        /// </summary>
        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Logger.Debug("DataContext changed detected");

            // Accept DataContext if it is the expected ViewModel type or has the same runtime name (avoids cross-assembly type identity issues)
            if (e.NewValue is BusBuddy.WPF.ViewModels.MainWindowViewModel newViewModel ||
                string.Equals(e.NewValue?.GetType()?.Name, nameof(BusBuddy.WPF.ViewModels.MainWindowViewModel), StringComparison.OrdinalIgnoreCase))
            {
                _viewModel = e.NewValue as BusBuddy.WPF.ViewModels.MainWindowViewModel ?? _viewModel ?? new BusBuddy.WPF.ViewModels.MainWindowViewModel();
                if (this.DataContext is not BusBuddy.WPF.ViewModels.MainWindowViewModel)
                {
                    this.DataContext = _viewModel;
                }
                Logger.Debug("DataContext updated to MainWindowViewModel (by type/name match)");
            }
            else if (e.NewValue == null)
            {
                Logger.Warning("DataContext was set to null, restoring previous ViewModel");
                if (_viewModel != null)
                {
                    this.DataContext = _viewModel;
                }
                else
                {
                    Logger.Warning("No previous ViewModel available, creating new one");
                    _viewModel = new BusBuddy.WPF.ViewModels.MainWindowViewModel();
                    this.DataContext = _viewModel;
                }
            }
            else
            {
                // Demote to debug to avoid noisy warnings when transient design-time contexts flow through
                Logger.Debug("DataContext changed to {Type}; retaining existing MainWindowViewModel", e.NewValue?.GetType()?.Name ?? "null");
            }
        }

        private void CreateFallbackLayout()
        {
            Logger.Debug("CreateFallbackLayout method started");
            Logger.Information("Creating fallback layout due to XAML initialization failure");

            // Simplified layout if XAML fails
            this.Width = 1200;
            this.Height = 800;
            this.Title = "BusBuddy - Transportation Management";

            var welcomeText = new TextBlock
            {
                Text = "BusBuddy MVP - Syncfusion Layout Loading...\n\nIf this message persists, check Syncfusion assembly references.",
                FontSize = 18,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };

            this.Content = welcomeText;
        }

        #region Navigation Button Click Handlers

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("DashboardButton_Click event triggered");
            Logger.Information("Dashboard navigation requested");
            // Future: Navigate to dashboard view
            Logger.Debug("Dashboard navigation logic completed");
        }

    private void ThemeSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBoxAdv combo && combo.SelectedItem is ComboBoxItemAdv item && item.Content is string theme)
                {
                    Logger.Information("Theme selection changed to {Theme} by {Component}", theme, GetType().Name);
                    // Apply to all open windows for consistency
                    ApplyThemeGlobally(theme);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed applying selected theme, attempting fallback");
                try
                {
                    ApplyThemeGlobally("FluentDark");
                }
                catch (Exception inner)
                {
                    Logger.Error(inner, "Failed to apply fallback theme");
                }
            }
        }

        // Quick theme toggle buttons (🌙 / ☀️)
        private void DarkThemeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("DarkThemeButton_Click invoked - applying FluentDark theme");
            ApplyThemeGlobally("FluentDark");
            TrySyncThemeSelector("FluentDark");
        }

        private void LightThemeButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("LightThemeButton_Click invoked - applying FluentLight theme");
            ApplyThemeGlobally("FluentLight");
            TrySyncThemeSelector("FluentLight");
        }

        private void TrySyncThemeSelector(string themeName)
        {
            try
            {
                if (ThemeSelector != null)
                {
                    for (int i = 0; i < ThemeSelector.Items.Count; i++)
                    {
                        if (ThemeSelector.Items[i] is ComboBoxItemAdv item && string.Equals(item.Content?.ToString(), themeName, StringComparison.OrdinalIgnoreCase))
                        {
                            ThemeSelector.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to synchronize ThemeSelector selection");
            }
        }

    // Removed malformed duplicate ApplyThemeGlobally block (corruption cleanup).

        /// <summary>
        /// Navigate to Students management view
        /// </summary>
        private void StudentsButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("StudentsButton_Click event triggered");
            Logger.Information("Students navigation requested");
            try
            {
                _navigationService?.Navigate("students");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Students view");
                MessageBox.Show($"Error opening Students view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Navigate to Route management view
        /// </summary>
        private void RouteManagementButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("RouteManagementButton_Click event triggered");
            Logger.Information("Route management navigation requested");
            try
            {
                _navigationService?.Navigate("routes");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Route Management view");
                MessageBox.Show($"Error opening Route Management view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Navigate to Drivers management view
        /// </summary>
        private void DriversButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("DriversButton_Click event triggered");
            Logger.Information("Drivers navigation requested");
            try
            {
                _navigationService?.Navigate("drivers");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Drivers view");
                MessageBox.Show($"Error opening Drivers view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Navigate to Buses management view
        /// </summary>
        private void BusesButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("BusesButton_Click event triggered");
            Logger.Information("Buses navigation requested");
            try
            {
                _navigationService?.Navigate("buses");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Bus Management view");
                MessageBox.Show($"Error opening Bus Management view: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Show / activate the Map (Google Earth) pane inside the DockingManager.
        /// </summary>
        private void MapButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("MapButton_Click event triggered");
            try
            {
                _navigationService?.Navigate("map");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error activating Map pane");
            }
        }

        /// <summary>
        /// Navigate to Reports view and trigger PrintRoutes
        /// </summary>
        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Opening Reports view and triggering PrintRoutes");
            try
            {
                // For MVP, directly trigger PrintRoutes without a separate view
                // Resolve via DI (RouteManagementViewModel registered in App.xaml.cs)
                var routeViewModel = App.ServiceProvider.GetRequiredService<BusBuddy.WPF.ViewModels.Route.RouteManagementViewModel>();

                // Use existing PrintScheduleCommand (PrintRoutesCommand not defined in current ViewModel)
                if (routeViewModel.PrintScheduleCommand?.CanExecute(null) == true)
                {
                    Logger.Information("Executing PrintSchedule command");
                    routeViewModel.PrintScheduleCommand.Execute(null);

                    MessageBox.Show("Route report generated successfully and saved to Desktop!",
                        "Report Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("PrintSchedule command is not available.",
                        "Command Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to execute PrintRoutes");
                // ShowErrorMessage expects (message, title)
                // Maintain original ShowErrorMessage signature (message, title)
                ShowErrorMessage(ex.Message, "Failed to generate route report");
            }
        }

        #endregion

        #region Action Button Click Handlers

        // MVP Button Click Handlers
        private void AddStudent_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("AddStudent_Click event triggered");
            try
            {
                Logger.Debug("Creating new StudentForm dialog");
                var studentForm = new StudentForm();
                Logger.Debug("Showing StudentForm hosted in transient window");
                var result = ShowUserControlDialog(studentForm, "Add Student");
                Logger.Debug("StudentForm dialog result: {DialogResult}", result);
                if (result == true)
                {
                    Logger.Information("Student added successfully");
                    Logger.Debug("Student form completed successfully, refreshing data");
                    RefreshStudentsGrid();
                }
                else
                {
                    Logger.Debug("Student form was cancelled or closed without saving");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Student form");
                MessageBox.Show($"Error opening Student form: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditStudent_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Edit student requested");
            try
            {
                // Get selected student through ViewModel to avoid direct grid access
                if (DataContext is not MainWindowViewModel mainViewModel)
                {
                    Logger.Warning("DataContext is not MainWindowViewModel, cannot access student data");
                    MessageBox.Show("Unable to access student data. Please try restarting the application.",
                        "Data Access Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Use grid's selected item if available, with fallback to ViewModel
                BusBuddy.Core.Models.Student? selectedStudent = null;

                try
                {
                    // Try to get selected item from grid
                    // Guard: StudentsGrid may be null if InitializeComponent was skipped
                    selectedStudent = null;
                }
                catch (Exception gridEx)
                {
                    Logger.Warning(gridEx, "Unable to access StudentsGrid directly, using ViewModel fallback");
                }

                // Fallback: use first student if no selection or grid access fails
                if (selectedStudent == null)
                {
                    // Use a safe approach to access students data
                    var studentsProperty = mainViewModel.GetType().GetProperty("Students");
                    if (studentsProperty != null)
                    {
                        var studentsCollection = studentsProperty.GetValue(mainViewModel) as System.Collections.ICollection;
                        if (studentsCollection != null && studentsCollection.Count > 0)
                        {
                            var studentsEnumerable = studentsCollection as System.Collections.IEnumerable;
                            foreach (var student in studentsEnumerable)
                            {
                                selectedStudent = student as BusBuddy.Core.Models.Student;
                                if (selectedStudent != null)
                                {
                                    Logger.Information("No student selected, using first student as fallback: {StudentName}",
                                        selectedStudent.StudentName);
                                    break;
                                }
                            }
                        }
                    }

                    if (selectedStudent == null)
                    {
                        MessageBox.Show("No students available to edit", "No Student Selected",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }

                Logger.Information("Opening StudentForm for editing student: {StudentName} (ID: {StudentId})",
                    selectedStudent.StudentName, selectedStudent.StudentId);

                var studentForm = new BusBuddy.WPF.Views.Student.StudentForm();

                // Set the DataContext to a new ViewModel with the selected student
                var studentViewModel = new BusBuddy.WPF.ViewModels.Student.StudentFormViewModel(selectedStudent);
                studentForm.DataContext = studentViewModel;

                var result = ShowUserControlDialog(studentForm, "Edit Student");
                if (result == true)
                {
                    Logger.Information("Student edited successfully");
                    RefreshStudentsGrid();
                    MessageBox.Show("Student updated successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Student form for editing");
                MessageBox.Show($"Error opening Student form for editing: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddBus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var busForm = new BusForm();
                var result = ShowUserControlDialog(busForm, "Add Bus");
                if (result == true)
                {
                    Logger.Information("Bus added successfully");
                    // TODO: Refresh bus grid
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening Bus form");
                MessageBox.Show($"Error opening Bus form: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDriver_Click(object sender, RoutedEventArgs e)
        {
            Logger.Information("Attempting to open DriverForm");
            try
            {
                Logger.Debug("Creating new DriverForm dialog");
                var driverForm = new DriverForm();
                Logger.Debug("Showing DriverForm hosted modal dialog");
                var result = ShowUserControlDialog(driverForm, "Add Driver");
                Logger.Debug("DriverForm hosted dialog result: {DialogResult}", result);
                if (result == true)
                {
                    Logger.Information("DriverForm opened successfully");
                    Logger.Debug("Driver form completed successfully, refreshing data");
                    RefreshDriversGrid();
                }
                else
                {
                    Logger.Debug("Driver form was cancelled or closed without saving");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error opening DriverForm");
                MessageBox.Show($"Error opening Driver form: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Routes panel event handlers
        private void OptimizeRoutes_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("OptimizeRoutes_Click event triggered");
            Logger.Information("Route optimization requested");
            try
            {
                MessageBox.Show("Route optimization feature will be implemented in next phase.\n\nComing soon:\n• AI-powered route optimization\n• Traffic pattern analysis\n• Fuel efficiency calculations",
                    "Route Optimization", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in route optimization");
            }
        }

        private void ExportSchedules_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("ExportSchedules_Click event triggered");
            Logger.Information("Schedule export requested");
            try
            {
                var exportService = App.ServiceProvider?.GetService<BusBuddy.WPF.Services.RouteExportService>();
                if (exportService == null)
                {
                    MessageBox.Show("Export service not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var csvTask = exportService.ExportRoutesToCsvAsync();
                var reportTask = exportService.GenerateRouteReportAsync();

                Task.Run(async () =>
                {
                    try
                    {
                        var csvPath = await csvTask;
                        var reportPath = await reportTask;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Schedules exported successfully!\n\nCSV Report: {csvPath}\nDetailed Report: {reportPath}",
                                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in schedule export");
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Bus panel event handlers
        private void Maintenance_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Maintenance_Click event triggered");
            Logger.Information("Maintenance management requested");
            try
            {
                MessageBox.Show("Maintenance management feature will be implemented in next phase.\n\nComing soon:\n• Scheduled maintenance tracking\n• Service history logs\n• Maintenance alerts and reminders",
                    "Maintenance Management", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in maintenance management");
            }
        }

        private void FleetStatus_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("FleetStatus_Click event triggered");
            Logger.Information("Fleet status requested");
            try
            {
                MessageBox.Show("Fleet status dashboard will be implemented in next phase.\n\nComing soon:\n• Real-time fleet monitoring\n• Vehicle location tracking\n• Performance metrics dashboard",
                    "Fleet Status", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in fleet status");
            }
        }

        // Driver panel event handlers
        private void AssignBus_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("AssignBus_Click event triggered");
            Logger.Information("Bus assignment requested");
            try
            {
                MessageBox.Show("Bus assignment feature will be implemented in next phase.\n\nComing soon:\n• Driver-to-bus assignments\n• Route scheduling\n• Automatic assignment optimization",
                    "Assign Bus", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in bus assignment");
            }
        }

        private void Schedule_Click(object sender, RoutedEventArgs e)
        {
            Logger.Debug("Schedule_Click event triggered");
            Logger.Information("Driver scheduling requested");
            try
            {
                MessageBox.Show("Driver scheduling feature will be implemented in next phase.\n\nComing soon:\n• Shift management\n• Availability tracking\n• Schedule conflict detection",
                    "Driver Scheduling", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error in driver scheduling");
            }
        }

        #endregion

        #region Data Refresh Methods

        private bool EnsureMainWindowViewModel()
        {
            try
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    _viewModel = vm;
                    return true;
                }

                // Defensive: type identity issues across reloads — match by name
                if (DataContext != null && string.Equals(DataContext.GetType().Name, nameof(MainWindowViewModel), StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel ??= new MainWindowViewModel();
                    DataContext = _viewModel; // normalize instance
                    Logger.Debug("Normalized DataContext to MainWindowViewModel by name match");
                    return true;
                }

                if (_viewModel == null)
                {
                    _viewModel = new MainWindowViewModel();
                    DataContext = _viewModel;
                    Logger.Information("Created new MainWindowViewModel in EnsureMainWindowViewModel");
                }
                else if (DataContext == null)
                {
                    DataContext = _viewModel;
                    Logger.Debug("Restored DataContext to existing MainWindowViewModel instance");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "EnsureMainWindowViewModel failed");
                return false;
            }
    }

        private void RefreshStudentsGrid()
        {
            try
            {
                if (!EnsureMainWindowViewModel()) return;
                // Use reflection to avoid tight coupling if property shape changes
                var prop = _viewModel?.GetType().GetProperty("LoadStudentsCommand") ?? _viewModel?.GetType().GetProperty("RefreshStudentsCommand");
                if (prop?.GetValue(_viewModel) is ICommand cmd && cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                    Logger.Information("Triggered students refresh via command");
                    return;
                }

                // Fallback: look for async method LoadStudentsAsync
                var loadMethod = _viewModel?.GetType().GetMethod("LoadStudentsAsync");
                if (loadMethod != null)
                {
                    var taskObj = loadMethod.Invoke(_viewModel, null) as Task;
                    taskObj?.ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            Logger.Warning(t.Exception, "Students refresh task faulted");
                    });
                    Logger.Information("Triggered students refresh via LoadStudentsAsync");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RefreshStudentsGrid failed");
            }
        }

        private void RefreshDriversGrid()
        {
            try
            {
                if (!EnsureMainWindowViewModel()) return;
                var prop = _viewModel?.GetType().GetProperty("LoadDriversCommand") ?? _viewModel?.GetType().GetProperty("RefreshDriversCommand");
                if (prop?.GetValue(_viewModel) is ICommand cmd && cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                    Logger.Information("Triggered drivers refresh via command");
                    return;
                }

                var loadMethod = _viewModel?.GetType().GetMethod("LoadDriversAsync");
                if (loadMethod != null)
                {
                    var taskObj = loadMethod.Invoke(_viewModel, null) as Task;
                    taskObj?.ContinueWith(t =>
                    {
                        if (t.Exception != null)
                            Logger.Warning(t.Exception, "Drivers refresh task faulted");
                    });
                    Logger.Information("Triggered drivers refresh via LoadDriversAsync");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "RefreshDriversGrid failed");
            }
        }

        #endregion

        #region Messaging Helpers
        private void ShowErrorMessage(string message, string title)
        {
            try
            {
                Logger.Error("UI Error: {Title} - {Message}", title, message);
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { /* swallow */ }
        }

        private void ShowInfoMessage(string message, string title)
        {
            try
            {
                Logger.Information("UI Info: {Title} - {Message}", title, message);
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void ShowWarningMessage(string message, string title)
        {
            try
            {
                Logger.Warning("UI Warning: {Title} - {Message}", title, message);
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch { }
        }
        #endregion

        #region Dialog Hosting Helpers
        private bool? ShowUserControlDialog(UserControl control, string title)
        {
            try
            {
                var host = new Window
                {
                    Title = title,
                    Content = control,
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MinWidth = 820,
                    MinHeight = 640
                };

                if (control is BusBuddy.WPF.Views.Student.StudentForm sf)
                {
                    sf.RequestCloseByHost += (s, _) => { host.DialogResult = sf.DialogResult; host.Close(); };
                }

                return host.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to host control in dialog: {Title}", title);
                MessageBox.Show($"Failed to open dialog: {ex.Message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        #endregion

    }
}
