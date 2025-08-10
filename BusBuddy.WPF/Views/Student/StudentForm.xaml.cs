using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Automation; // AutomationProperties for accessibility checks
using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion WPF docs
using Syncfusion.SfSkinManager; // SfSkinManager per official docs
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.WPF.Utilities; // SyncfusionThemeManager
using Serilog;

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
    public partial class StudentForm : ChromelessWindow
    {
    private static readonly ILogger Logger = Log.ForContext<StudentForm>();
        public StudentFormViewModel ViewModel { get; private set; }

    /// <summary>
    /// Default constructor: initializes theming, ViewModel, and event hooks.
    /// </summary>
    public StudentForm()
        {
            InitializeComponent();
            // Apply Syncfusion theme via central manager (FluentDark with FluentLight fallback)
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);
            ViewModel = new StudentFormViewModel();
            DataContext = ViewModel;

            // Subscribe to ViewModel events for form closure
            // (Allows ViewModel to close dialog on save/cancel)
            ViewModel.RequestClose += OnRequestClose;

            // Window lifecycle diagnostics — useful to trace load/render timing
            try
            {
                Loaded += OnLoaded;
                ContentRendered += OnContentRendered;
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach window lifecycle diagnostics");
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
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach text diagnostics");
            }

            // Validation diagnostics — logs when errors are added/removed
            try
            {
                AddHandler(Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
                Logger.Information("StudentForm: validation diagnostics attached");
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach validation diagnostics");
            }

            Logger.Information("StudentForm initialized (Create mode)");
        }

    /// <summary>
    /// Overload: initializes with an existing student for editing.
    /// </summary>
    public StudentForm(Core.Models.Student student) : this()
        {
            ViewModel = new StudentFormViewModel(student);
            DataContext = ViewModel;
            ViewModel.RequestClose += OnRequestClose;
            Logger.Information("StudentForm initialized (Edit mode) for StudentId={StudentId}", student.StudentId);
        }

    /// <summary>
    /// Handles ViewModel RequestClose event to close dialog with result.
    /// </summary>
    private void OnRequestClose(object? sender, bool? dialogResult)
        {
            Logger.Information("StudentForm RequestClose received. DialogResult={DialogResult}", dialogResult);
            DialogResult = dialogResult;
            Close();
        }

    /// <summary>
    /// Cleanup: Unsubscribes events, disposes ViewModel, and releases SkinManager resources.
    /// </summary>
    protected override void OnClosed(System.EventArgs e)
        {
            Logger.Information("StudentForm closing, disposing resources");
            // Unsubscribe from events to prevent memory leaks
            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnRequestClose;
                ViewModel.Dispose();
            }
            // Remove global handlers where applicable
            try
            {
                Loaded -= OnLoaded;
                ContentRendered -= OnContentRendered;
                RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick));
                RemoveHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged));
                RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged));
                RemoveHandler(Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError));
            }
            catch { /* Best-effort cleanup */ }
            // Clear SkinManager instances for this window per docs
            try { SfSkinManager.Dispose(this); } catch { }
            base.OnClosed(e);
        }

        /// <summary>
        /// Logs that the window has finished loading.
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Logger.Information("StudentForm Loaded — DataContextType={DataContextType}", DataContext?.GetType().Name ?? "(null)");
        }

        /// <summary>
        /// Logs when the visual tree has been rendered.
        /// </summary>
        private void OnContentRendered(object? sender, System.EventArgs e)
        {
            Logger.Information("StudentForm ContentRendered — Ready for user interaction");
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
    }
}
