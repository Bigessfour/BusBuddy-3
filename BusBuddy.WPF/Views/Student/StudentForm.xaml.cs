using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media.TextFormatting;
using System.Windows.Automation; // AutomationProperties for accessibility checks
// Removed ChromelessWindow inheritance; now a UserControl hosted by parent window/dialog.
using Syncfusion.SfSkinManager; // SfSkinManager per official docs
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.WPF.Utilities; // SyncfusionThemeManager
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services;

namespace BusBuddy.WPF.Views.Student
{
    /// <summary>
    /// Interaction logic for StudentForm.xaml
    /// </summary>
    /// <summary>
    /// StudentForm — Syncfusion ChromelessWindow for student entry/edit.
    /// Applies FluentDark theme by default, falls back to FluentLight if needed.
    /// All controls are styled for accessibility and data type clarity.
    /// ViewModel handles all data and validation logic.
    /// Diagnostics: logs key UI interactions (button clicks, selection/text changes, and validation errors).
    /// </summary>
    public partial class StudentForm : UserControl, BusBuddy.WPF.Views.Common.IDialogHostable
    {
        private static readonly ILogger Logger = Log.ForContext<StudentForm>();
        public StudentFormViewModel ViewModel { get; private set; }
    private bool _isDirty;
        public bool? DialogResult { get; private set; }

        /// <summary>
        /// Default constructor: initializes theming, ViewModel, and event hooks.
        /// </summary>
        public StudentForm()
        {
            InitializeComponent();
            // Apply Syncfusion theme via central manager (FluentDark with FluentLight fallback)
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);

            // High DPI defaults for crisp rendering
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            // Resolve IStudentService from DI if available
            try
            {
                var sp = App.ServiceProvider;
                var svc = sp?.GetService<IStudentService>();
                ViewModel = svc != null ? new StudentFormViewModel(svc) : new StudentFormViewModel();
            }
            catch
            {
                ViewModel = new StudentFormViewModel();
            }
            DataContext = ViewModel;
            TryAttachGlobalErrorListener();

            // Subscribe to ViewModel events for form closure
            // (Allows ViewModel to close dialog on save/cancel)
            ViewModel.RequestClose += OnRequestClose;

            // Control lifecycle diagnostics — Loaded + (defer ContentRendered equivalent via dispatcher)
            try
            {
                Loaded += OnLoaded;
                // Simulate ContentRendered after first layout pass
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { OnContentRendered(this, System.EventArgs.Empty); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach lifecycle diagnostics");
            }

            // Global button click diagnostics for this dialog (bubbling handler)
            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                Logger.Information("StudentForm: global button click diagnostics attached");
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach button diagnostics");
            }

            // Global selection change diagnostics for Selector-based controls (ComboBox, ListBox, etc.)
            try
            {
                AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged), true);
                Logger.Information("StudentForm: global selection change diagnostics attached");
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach selection diagnostics");
            }

            // Global text change diagnostics for text inputs — logs length only (no PII)
            try
            {
                AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true);
                Logger.Information("StudentForm: global text change diagnostics attached");
                // Mark form dirty on any text change
                AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => _isDirty = true), true);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach text diagnostics");
            }

            // Validation diagnostics — logs when errors are added/removed
            try
            {
                AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
                Logger.Information("StudentForm: validation diagnostics attached");
                // Also consider any validation error as a change indicator
                AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>((s, e) => _isDirty = true), true);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach validation diagnostics");
            }

            Logger.Information("StudentForm (UserControl) initialized (Create mode)");
        }

        /// <summary>
        /// Overload: initializes with an existing student for editing.
        /// </summary>
        public StudentForm(Core.Models.Student student) : this()
        {
            try
            {
                var sp = App.ServiceProvider;
                var svc = sp?.GetService<IStudentService>();
                ViewModel = svc != null ? new StudentFormViewModel(svc, student, enableValidation: false) : new StudentFormViewModel(student, enableValidation: false);
            }
            catch
            {
                ViewModel = new StudentFormViewModel(student, enableValidation: false);
            }
            DataContext = ViewModel;
            TryAttachGlobalErrorListener();
            ViewModel.RequestClose += OnRequestClose;
            Logger.Information("StudentForm (UserControl) initialized (Edit mode) for StudentId={StudentId}", student.StudentId);
        }

        private void TryAttachGlobalErrorListener()
        {
            try
            {
                if (ViewModel == null) return;
                ViewModel.PropertyChanged += (s, e) =>
                {
                    try
                    {
                        if (e.PropertyName == nameof(ViewModel.HasGlobalError) && ViewModel.HasGlobalError)
                        {
                            var msg = ViewModel.GlobalErrorMessage ?? "An error occurred.";
                            if (ViewModel.HasValidationErrors && ViewModel.ValidationErrors.Count > 0)
                            {
                                var first = ViewModel.ValidationErrors.Take(3).ToArray();
                                msg += "\n\nDetails:" + "\n" + string.Join("\n", first);
                                if (ViewModel.ValidationErrors.Count > 3)
                                {
                                    msg += $"\n(+{ViewModel.ValidationErrors.Count - 3} more)";
                                }
                            }
                            MessageBox.Show(msg, "Validation error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.Warning(ex, "StudentForm: error listener failed");
                    }
                };
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach global error listener");
            }
        }

        /// <summary>
        /// Handles ViewModel RequestClose event to close dialog with result.
        /// </summary>
        private void OnRequestClose(object? sender, bool? dialogResult)
        {
            Logger.Information("StudentForm RequestClose received. DialogResult={DialogResult}", dialogResult);
            DialogResult = dialogResult;
            RequestCloseByHost?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler? RequestCloseByHost;

        // Prompt to save if there are unsaved changes
    // Removed OnClosingPromptSave; hosting window should handle unsaved prompt before closing.

        /// <summary>
        /// Cleanup: Unsubscribes events, disposes ViewModel, and releases SkinManager resources.
        /// </summary>
        public void DisposeResources()
        {
            Logger.Information("StudentForm disposing resources");
            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnRequestClose;
                ViewModel.Dispose();
            }
            try
            {
                Loaded -= OnLoaded;
                RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick));
                RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged));
                RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged));
                RemoveHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError));
            }
            catch { }
            try { SfSkinManager.Dispose(this); } catch { }
        }

        /// <summary>
        /// Logs that the window has finished loading.
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("StudentForm Loaded (UserControl) — DataContextType={DataContextType}", DataContext?.GetType().Name ?? "(null)");
        }

        /// <summary>
        /// Logs when the visual tree has been rendered.
        /// </summary>
        private void OnContentRendered(object? sender, System.EventArgs e)
        {
            Logger.Information("StudentForm ContentReady — Ready for user interaction");
            // One-time UI audit after visual tree is ready
            try { AuditButtonsAccessibility(); }
            catch (System.Exception ex) { Logger.Warning(ex, "StudentForm: UI audit failed"); }
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
                    bool? canExec = null;
                    try { if (badv.Command != null) canExec = badv.Command.CanExecute(badv.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(badv);
                    var help = AutomationProperties.GetHelpText(badv);
                    Logger.Information(
                        "StudentForm Button: {Type} Name={Name} Label={Label} AutoName={AutoName} Help={Help} IsEnabled={IsEnabled} HasCommand={HasCommand} CanExecute={CanExecute}",
                        type, name, badv.Label, autoName, help, badv.IsEnabled, badv.Command != null, canExec);

                    // Accessibility warning if label is missing and no automation name
                    if (string.IsNullOrWhiteSpace(badv.Label) && string.IsNullOrWhiteSpace(autoName))
                    {
                        Logger.Warning("StudentForm ButtonAdv missing label and AutomationProperties.Name — Name={Name}", name);
                    }
                }
                else if (src is Button btn)
                {
                    bool? canExec = null;
                    try { if (btn.Command != null) canExec = btn.Command.CanExecute(btn.CommandParameter); } catch { }
                    var autoName = AutomationProperties.GetName(btn);
                    var help = AutomationProperties.GetHelpText(btn);
                    var contentText = btn.Content?.ToString();
                    Logger.Information(
                        "StudentForm Button: {Type} Name={Name} Content={Content} AutoName={AutoName} Help={Help} IsEnabled={IsEnabled} HasCommand={HasCommand} CanExecute={CanExecute}",
                        type, name, contentText, autoName, help, btn.IsEnabled, btn.Command != null, canExec);

                    if (string.IsNullOrWhiteSpace(contentText) && string.IsNullOrWhiteSpace(autoName))
                    {
                        Logger.Warning("StudentForm Button missing Content and AutomationProperties.Name — Name={Name}", name);
                    }
                }
                else
                {
                    Logger.Information("StudentForm Button: {Type} Name={Name}", type, name);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: button click logging failed");
            }

            // After any button click, if the ViewModel has surfaced a global error, show it immediately.
            try
            {
                if (ViewModel?.HasGlobalError == true && !string.IsNullOrWhiteSpace(ViewModel.GlobalErrorMessage))
                {
                    // If there are detailed validation errors, include a compact hint
                    string message = ViewModel.GlobalErrorMessage;
                    if (ViewModel.HasValidationErrors && ViewModel.ValidationErrors.Count > 0)
                    {
                        // Show only first 3 to keep dialog concise
                        var first = ViewModel.ValidationErrors.Take(3).ToArray();
                        message += "\n\nDetails:" + "\n" + string.Join("\n", first);
                        if (ViewModel.ValidationErrors.Count > 3)
                        {
                            message += $"\n(+{ViewModel.ValidationErrors.Count - 3} more)";
                        }
                    }

                    MessageBox.Show(message, "Action blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to display global error message");
            }
        }

        /// <summary>
        /// Audits Button and ButtonAdv elements for missing labels/names and basic command wiring.
        /// Runs once after ContentRendered when the visual tree is ready.
        /// </summary>
        private void AuditButtonsAccessibility()
        {
            int total = 0, advCount = 0, missingLabel = 0, missingName = 0, noCommand = 0;

            foreach (var d in Traverse(this))
            {
                if (d is Syncfusion.Windows.Tools.Controls.ButtonAdv badv)
                {
                    total++; advCount++;
                    var label = badv.Label;
                    var autoName = AutomationProperties.GetName(badv);
                    var help = AutomationProperties.GetHelpText(badv);
                    bool hasCommand = badv.Command != null;
                    bool? canExec = null; try { if (hasCommand) canExec = badv.Command?.CanExecute(badv.CommandParameter); } catch { }

                    if (string.IsNullOrWhiteSpace(label)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingName++;
                    if (!hasCommand) noCommand++;

                    Logger.Information(
                        "UI Audit — ButtonAdv Name={Name} Label={Label} AutoName={AutoName} Help={Help} IsEnabled={IsEnabled} HasCommand={HasCommand} CanExecute={CanExecute}",
                        (badv as FrameworkElement)?.Name ?? "(unnamed)", label, autoName, help, badv.IsEnabled, hasCommand, canExec);

                    if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(autoName))
                    {
                        Logger.Warning("UI Audit — ButtonAdv missing both Label and AutomationProperties.Name: {Name}", (badv as FrameworkElement)?.Name ?? "(unnamed)");
                    }
                }
                else if (d is Button btn)
                {
                    total++;
                    var contentText = btn.Content?.ToString();
                    var autoName = AutomationProperties.GetName(btn);
                    var help = AutomationProperties.GetHelpText(btn);
                    bool hasCommand = btn.Command != null;
                    bool? canExec = null; try { if (hasCommand) canExec = btn.Command?.CanExecute(btn.CommandParameter); } catch { }

                    if (string.IsNullOrWhiteSpace(contentText)) missingLabel++;
                    if (string.IsNullOrWhiteSpace(autoName)) missingName++;
                    if (!hasCommand) noCommand++;

                    Logger.Information(
                        "UI Audit — Button Name={Name} Content={Content} AutoName={AutoName} Help={Help} IsEnabled={IsEnabled} HasCommand={HasCommand} CanExecute={CanExecute}",
                        btn.Name ?? "(unnamed)", contentText, autoName, help, btn.IsEnabled, hasCommand, canExec);

                    if (string.IsNullOrWhiteSpace(contentText) && string.IsNullOrWhiteSpace(autoName))
                    {
                        Logger.Warning("UI Audit — Button missing both Content and AutomationProperties.Name: {Name}", btn.Name ?? "(unnamed)");
                    }
                }
            }

            Logger.Information("UI Audit Summary — Buttons={Total}, ButtonAdv={AdvCount}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingName}, NoCommand={NoCommand}",
                total, advCount, missingLabel, missingName, noCommand);
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

        /// <summary>
        /// Logs selection changes for Selector-based controls (e.g., ComboBox, ListBox).
        /// </summary>
        private void OnAnySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                string name = fe?.Name ?? "(unnamed)";
                string type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");

                // Attempt to get current selection details without logging PII
                int added = e.AddedItems?.Count ?? 0;
                int removed = e.RemovedItems?.Count ?? 0;

                int? selectedIndex = null;
                string? selectedType = null;
                if (sender is Selector selector)
                {
                    selectedIndex = selector is ComboBox cb ? cb.SelectedIndex : selector.SelectedIndex;
                    var item = (selector as dynamic)?.SelectedItem; // best-effort
                    selectedType = item?.GetType().Name;
                }

                Logger.Information(
                    "StudentForm SelectionChanged: {Type} Name={Name} Added={Added} Removed={Removed} SelectedIndex={SelectedIndex} SelectedItemType={SelectedItemType}",
                    type, name, added, removed, selectedIndex, selectedType);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: selection change logging failed");
            }
        }

        /// <summary>
        /// Logs text changes for text input controls — logs length only (no content).
        /// </summary>
        private void OnAnyTextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                if (e.OriginalSource is not DependencyObject src) return;
                var fe = src as FrameworkElement;
                string name = fe?.Name ?? "(unnamed)";
                string type = src.GetType().Name;

                int? length = null;
                if (src is TextBox tb)
                {
                    length = tb.Text?.Length ?? 0;
                }
                else if (src is PasswordBox pb)
                {
                    length = pb.Password?.Length ?? 0; // length only
                }

                Logger.Information("StudentForm TextChanged: {Type} Name={Name} Length={Length}", type, name, length);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: text change logging failed");
            }
        }

        /// <summary>
        /// Logs WPF validation errors being added/removed from controls.
        /// </summary>
        private void OnValidationError(object? sender, ValidationErrorEventArgs e)
        {
            try
            {
                var src = e.OriginalSource as DependencyObject;
                var fe = src as FrameworkElement;
                string name = fe?.Name ?? "(unnamed)";
                string type = src?.GetType().Name ?? (sender?.GetType().Name ?? "(unknown)");

                var action = e.Action.ToString();
                var error = e.Error;
                string? errorContent = error?.ErrorContent?.ToString();
                string? bindingExpr = error?.BindingInError?.ToString();

                Logger.Warning("StudentForm Validation{Action}: {Type} Name={Name} Error={Error} Binding={Binding}",
                    action, type, name, errorContent, bindingExpr);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: validation logging failed");
            }
        }

        // Handle per-monitor DPI changes to keep layout and fonts crisp
    // Removed OnDpiChanged override (specific to Window). Host window should manage DPI adjustments.
    }
}
