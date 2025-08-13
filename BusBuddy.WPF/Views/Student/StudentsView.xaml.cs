using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Automation;
using System.Windows.Media.TextFormatting;
using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion docs
using Syncfusion.SfSkinManager; // For Syncfusion theming
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.WPF.Utilities; // SyncfusionThemeManager
using Serilog; // Logging per project standards
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics; // Conditional/DEBUG — https://learn.microsoft.com/dotnet/api/system.diagnostics.conditionalattribute
using BusBuddy.Core.Data; // For IBusBuddyDbContextFactory
using BusBuddy.Core.Services; // For AddressService fallback when DI unavailable

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// StudentsView — Student management view using Syncfusion ChromelessWindow with theming.
    /// Docs:
    /// - Syncfusion ChromelessWindow: https://help.syncfusion.com/wpf/chromelesswindow/overview
    /// - Syncfusion SfSkinManager: https://help.syncfusion.com/wpf/themes/sfskinmanager
    /// - WPF RenderOptions/TextOptions: https://learn.microsoft.com/dotnet/api/system.windows.media.renderoptions
    /// - WPF DPI handling (OnDpiChanged): https://learn.microsoft.com/dotnet/api/system.windows.window.ondpichanged
    /// </summary>
    // Converted from ChromelessWindow to UserControl for docking document hosting (Syncfusion DockingManager expects UserControl documents)
    public partial class StudentsView : UserControl
    {
        private static readonly ILogger Logger = Log.ForContext<StudentsView>();
        public StudentsView()
        {
            InitializeComponent();

            // Apply Syncfusion theme (UserControl variant)
            // Docs: SfSkinManager — https://help.syncfusion.com/wpf/themes/sfskinmanager
            try
            {
                SfSkinManager.ApplyThemeAsDefaultStyle = true;
                SyncfusionThemeManager.ApplyTheme(this);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: theme application failed (non-fatal)");
            }

            // Set the ViewModel for data binding (prefer DI so DbContext/connection align with saves)
            try
            {
                var sp = App.ServiceProvider;
                var vm = sp?.GetService<StudentsViewModel>();
                // Prefer DI-provided VM. If unavailable, still attempt to use DI factory to ensure
                // connection strings align with appsettings; lastly fall back to default ctor.
                if (vm != null)
                {
                    DataContext = vm;
                }
                else if (sp != null)
                {
                    var factory = sp.GetService<IBusBuddyDbContextFactory>() ?? new BusBuddyDbContextFactory();
                    DataContext = new StudentsViewModel(factory, sp.GetService<AddressService>() ?? new AddressService());
                }
                else
                {
                    DataContext = new StudentsViewModel();
                }
            }
            catch (System.Exception ex)
            {
                // Serilog exception logging per project standards
                Logger.Warning(ex, "StudentsView: DI resolve failed — falling back to default StudentsViewModel");
                DataContext = new StudentsViewModel();
            }

            // Global interaction diagnostics (button, selection, text, validation) for observability
            try { AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true); } catch { }
            try { AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged), true); } catch { }
            try { AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true); } catch { }
            try { AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true); } catch { }
            Logger.Information("StudentsView: interaction diagnostics attached");

            // Ensure crisp images/text on high DPI
            // Docs: RenderOptions/TextOptions — https://learn.microsoft.com/dotnet/api/system.windows.media.renderoptions
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
            RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            Logger.Information("StudentsView initialized");

            // Inspect DataContext and command readiness on load
            this.Loaded += OnLoaded;

        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as BusBuddy.WPF.ViewModels.Student.StudentsViewModel;
                Logger.Information("StudentsView Loaded. DataContext={VM}", vm?.GetType().FullName ?? "(null)");
                if (vm != null)
                {
                    var addReady = vm.AddStudentCommand != null;
                    var editReady = vm.EditStudentCommand != null;
                    var delReady = vm.DeleteStudentCommand != null;
                    Logger.Information("Command readiness — Add:{Add} Edit:{Edit} Delete:{Del}", addReady, editReady, delReady);

                    // If no selection yet, select first row to enable edit/delete
                    if (vm.SelectedStudent == null && vm.Students.Count > 0)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { StudentsDataGrid.SelectedIndex = 0; } catch { }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }

                    // One-time delayed refresh to pick up background seeding that may complete shortly after startup.
                    _ = Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            await System.Threading.Tasks.Task.Delay(1500);
                            if (vm.RefreshCommand?.CanExecute(null) == true)
                            {
                                vm.RefreshCommand.Execute(null);
                                Logger.Information("StudentsView: Performed delayed refresh after load");
                            }
                        }
                        catch (System.Exception ex2)
                        {
                            Logger.Warning(ex2, "StudentsView: delayed refresh failed");
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: OnLoaded diagnostics failed");
            }

            // Schedule an accessibility audit once visual tree is fully realized
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { AuditButtonsAccessibility(); } catch (System.Exception ex2) { Logger.Warning(ex2, "StudentsView: accessibility audit failed"); }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch { }
        }

    private void OnAnyButtonClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                string name = fe?.Name ?? "(unnamed)";
                string type = src?.GetType().Name ?? "(unknown)";

                if (src is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    bool? canExec = null; try { if (badv.Command != null) canExec = badv.Command.CanExecute(badv.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(badv);
                    Logger.Information("StudentsView ButtonAdv: Type={Type} Name={Name} Label={Label} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", type, name, badv.Label, autoName, badv.Command != null, canExec);
                }
                else if (src is Button btn)
                {
                    bool? canExec = null; try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    Logger.Information("StudentsView Button: Type={Type} Name={Name} Content={Content} AutoName={AutoName} HasCommand={HasCommand} CanExecute={CanExecute}", type, name, btn.Content?.ToString(), autoName, btn.Command != null, canExec);
                }
                else
                {
                    Logger.Information("StudentsView Click: Type={Type} Name={Name}", type, name);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: button click logging failed");
            }
        }

    private void OnAnySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");
                Logger.Information("StudentsView SelectionChanged: Type={Type} Name={Name} Added={Added} Removed={Removed}", type, name, e.AddedItems?.Count ?? 0, e.RemovedItems?.Count ?? 0);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: selection change logging failed");
            }
        }

    private void OnAnyTextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is not DependencyObject src) return;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src.GetType().Name;
                int? len = src is TextBox tb ? tb.Text?.Length : null;
                Logger.Information("StudentsView TextChanged: Type={Type} Name={Name} Length={Length}", type, name, len);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: text change logging failed");
            }
        }

    private void OnValidationError(object? sender, ValidationErrorEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                var name = fe?.Name ?? "(unnamed)";
                var type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");
                Logger.Warning("StudentsView Validation{Action}: Type={Type} Name={Name} Error={Error}", e.Action, type, name, e.Error?.ErrorContent);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentsView: validation logging failed");
            }
        }

        private void AuditButtonsAccessibility()
        {
            int total = 0, adv = 0, missingLabel = 0, missingAuto = 0, noCmd = 0;
            foreach (var d in Traverse(this))
            {
                if (d is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    total++; adv++;
                    var label = badv.Label; var autoName = AutomationProperties.GetName(badv);
                    bool hasCmd = badv.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(label)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                }
                else if (d is Button btn)
                {
                    total++;
                    var content = btn.Content?.ToString(); var autoName = AutomationProperties.GetName(btn);
                    bool hasCmd = btn.Command != null; if (!hasCmd) noCmd++;
                    if (string.IsNullOrWhiteSpace(content)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingAuto++;
                }
            }
            Logger.Information("StudentsView Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}", total, adv, missingLabel, missingAuto, noCmd);
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Traverse(DependencyObject root)
        {
            if (root == null) yield break;
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child == null) continue;
                yield return child;
                foreach (var g in Traverse(child)) yield return g;
            }
        }

    // Window-specific overrides (OnDpiChanged, OnClosed) removed after conversion to UserControl.
    }
}
