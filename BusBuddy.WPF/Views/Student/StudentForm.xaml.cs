using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media.TextFormatting;
using System.Windows.Automation; // AutomationProperties for accessibility checks
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Shared; // ChromelessWindow per Syncfusion WPF docs
using Syncfusion.SfSkinManager; // SfSkinManager per official docs
using BusBuddy.WPF.ViewModels.Student;
using BusBuddy.WPF.Utilities; // SyncfusionThemeManager
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.GoogleMaps;

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
        private bool _isDirty;

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
                ViewModel = svc != null
                    ? new StudentFormViewModel(svc)
                    : new StudentFormViewModel();
                if (svc is null)
                {
                    Logger.Warning("StudentForm: IStudentService not in DI — saves may skip service validation");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "StudentForm: DI resolve failed — using fallback ViewModel");
                ViewModel = new StudentFormViewModel();
            }
            DataContext = ViewModel;
            WireStudentFormChrome();
            Logger.Information("StudentForm initialized (Create mode)");
        }

        /// <summary>
        /// Overload: initializes with an existing student for editing.
        /// </summary>
        public StudentForm(Core.Models.Student student)
        {
            InitializeComponent();
            SfSkinManager.ApplyThemeAsDefaultStyle = true;
            SyncfusionThemeManager.ApplyTheme(this);

            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.Fant);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            try
            {
                var sp = App.ServiceProvider;
                var svc = sp?.GetService<IStudentService>();
                ViewModel = svc != null
                    ? new StudentFormViewModel(svc, student, enableValidation: false)
                    : new StudentFormViewModel(student, enableValidation: false);
                if (svc is null)
                {
                    Logger.Warning("StudentForm: IStudentService not in DI — saves may skip service validation");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "StudentForm: DI resolve failed — using fallback ViewModel");
                ViewModel = new StudentFormViewModel(student, enableValidation: false);
            }

            DataContext = ViewModel;
            WireStudentFormChrome();
            Logger.Information("StudentForm initialized (Edit mode) for StudentId={StudentId}", student.StudentId);
        }

        private void WireStudentFormChrome()
        {
            ViewModel.RequestClose += OnRequestClose;
            ViewModel.RequestFocusField += OnRequestFocusField;

            try
            {
                Loaded += OnLoaded;
                ContentRendered += OnContentRendered;
                Closing += OnClosingPromptSave;
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach window lifecycle diagnostics");
            }

            try
            {
                AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick), true);
                AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged), true);
                AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnAnyTextChanged), true);
                AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((s, e) => _isDirty = true), true);
                AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError), true);
                AddHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>((s, e) => _isDirty = true), true);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: failed to attach diagnostics handlers");
            }
        }

        private void OnRequestFocusField(object? sender, string fieldKey)
        {
            Dispatcher.BeginInvoke(() => FocusFieldByKey(fieldKey), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void FocusFieldByKey(string fieldKey)
        {
            FrameworkElement? target = fieldKey switch
            {
                StudentFormFields.StudentName => StudentNameTextBox,
                StudentFormFields.Grade => GradeComboBox,
                StudentFormFields.HomeAddress => HomeAddressTextBox,
                StudentFormFields.City => CityTextBox,
                StudentFormFields.State => StateComboBox,
                StudentFormFields.Zip => ZipMaskedEdit,
                StudentFormFields.DateOfBirth => DateOfBirthPicker,
                StudentFormFields.AMRoute => AMRouteComboBox,
                StudentFormFields.PMRoute => PMRouteComboBox,
                StudentFormFields.HomePhone => HomePhoneMaskedEdit,
                StudentFormFields.CellPhone => CellPhoneMaskedEdit,
                StudentFormFields.EmergencyPhone => EmergencyContactPhoneMaskedEdit,
                StudentFormFields.School => SchoolComboBox,
                _ => null,
            };

            if (target is null)
            {
                return;
            }

            if (TryFocusInnerEditable(target))
            {
                return;
            }

            if (!target.Focus())
            {
                Keyboard.Focus(target);
            }

            if (target is TextBox textBox)
            {
                textBox.CaretIndex = textBox.Text?.Length ?? 0;
            }
        }

        private static bool TryFocusInnerEditable(FrameworkElement target)
        {
            TextBox? inner = target switch
            {
                SfMaskedEdit maskedEdit => maskedEdit.Template?.FindName("PART_TextBox", maskedEdit) as TextBox,
                SfTextBoxExt textExt => textExt.Template?.FindName("PART_TextBox", textExt) as TextBox,
                _ => null,
            };

            if (inner is null)
            {
                return false;
            }

            inner.Focus();
            inner.CaretIndex = inner.Text?.Length ?? 0;
            return true;
        }

        private void ClearFieldErrorForControl(string? controlName)
        {
            if (ViewModel is null || string.IsNullOrWhiteSpace(controlName))
            {
                return;
            }

            var fieldKey = controlName switch
            {
                nameof(StudentNameTextBox) => StudentFormFields.StudentName,
                nameof(GradeComboBox) => StudentFormFields.Grade,
                nameof(HomeAddressTextBox) => StudentFormFields.HomeAddress,
                nameof(CityTextBox) => StudentFormFields.City,
                nameof(StateComboBox) => StudentFormFields.State,
                nameof(ZipMaskedEdit) => StudentFormFields.Zip,
                nameof(DateOfBirthPicker) => StudentFormFields.DateOfBirth,
                nameof(AMRouteComboBox) => StudentFormFields.AMRoute,
                nameof(PMRouteComboBox) => StudentFormFields.PMRoute,
                nameof(HomePhoneMaskedEdit) => StudentFormFields.HomePhone,
                nameof(CellPhoneMaskedEdit) => StudentFormFields.CellPhone,
                nameof(EmergencyContactPhoneMaskedEdit) => StudentFormFields.EmergencyPhone,
                nameof(SchoolComboBox) => StudentFormFields.School,
                _ => null,
            };

            if (fieldKey is not null)
            {
                ViewModel.ClearFieldError(fieldKey);
            }
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

        // Prompt to save if there are unsaved changes
        private void OnClosingPromptSave(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (!_isDirty) return; // nothing to do
                // If already saved in this session, skip prompt
                if (DialogResult == true) return;

                var result = MessageBox.Show(
                    this,
                    "You have unsaved changes. Do you want to save before closing?",
                    "Save Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Trigger ViewModel save and cancel the close until it completes
                        e.Cancel = true;
                        _ = ViewModel?.GetType(); // null-guard
                        if (ViewModel?.SaveCommand?.CanExecute(null) == true)
                        {
                            ViewModel.SaveCommand.Execute(null);
                            // ViewModel should close window on success via RequestClose(true)
                        }
                        else
                        {
                            // If cannot save, keep the window open
                            MessageBox.Show(this, "Cannot save — please fix validation errors first.", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.Error(ex, "Save during closing failed");
                        MessageBox.Show(this, $"Error while saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true;
                    }
                }
                // No -> allow closing and discard changes
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: closing prompt encountered an error");
            }
        }

        /// <summary>
        /// Cleanup: Unsubscribes events, disposes ViewModel, and releases SkinManager resources.
        /// </summary>
        protected override void OnClosed(System.EventArgs e)
        {
            Logger.Information("StudentForm closing, disposing resources");
            if (ViewModel != null)
            {
                ViewModel.RequestClose -= OnRequestClose;
                ViewModel.RequestFocusField -= OnRequestFocusField;
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
                RemoveHandler(System.Windows.Controls.Validation.ErrorEvent, new EventHandler<ValidationErrorEventArgs>(OnValidationError));
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
            FitDialogToWorkArea();
            Title = $"Student Form · {ViewModel.FormTitle}";
            Logger.Information("StudentForm Loaded — DataContextType={DataContextType} FormTitle={FormTitle}",
                DataContext?.GetType().Name ?? "(null)", ViewModel.FormTitle);
            Dispatcher.BeginInvoke(() => FocusFieldByKey(StudentFormFields.StudentName), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void FitDialogToWorkArea()
        {
            var workArea = SystemParameters.WorkArea;
            MaxHeight = workArea.Height * 0.94;
            MaxWidth = Math.Min(980, workArea.Width * 0.96);
            if (Height > MaxHeight)
            {
                Height = MaxHeight;
            }

            if (Width > MaxWidth)
            {
                Width = MaxWidth;
            }
        }

        /// <summary>
        /// Logs when the visual tree has been rendered.
        /// </summary>
        private void OnContentRendered(object? sender, System.EventArgs e)
        {
            Logger.Information("StudentForm ContentRendered — Ready for user interaction");
            InputCaretHelper.RefreshCaretsInSubtree(this);
            // One-time UI audit after visual tree is ready
            try { AuditButtonsAccessibility(); }
            catch (System.Exception ex) { Logger.Warning(ex, "StudentForm: UI audit failed"); }
        }

        private void StudentForm_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            NumpadInputHelper.HandlePreviewKeyDown(e);
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
                ClearFieldErrorForControl(name);
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
                ClearFieldErrorForControl(name);
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

        private async void AddressSuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel is null || AddressSuggestionsList.SelectedItem is not PlaceAutocompleteSuggestion suggestion)
            {
                return;
            }

            try
            {
                await ViewModel.ApplyAddressSuggestionAsync(suggestion).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "StudentForm: Places suggestion apply failed");
            }
            finally
            {
                AddressSuggestionsList.SelectedItem = null;
            }
        }

        // Handle per-monitor DPI changes to keep layout and fonts crisp
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            try
            {
                var scale = newDpi.DpiScaleX; // assume uniform X/Y scaling
                // Adjust window-level font size for controls inheriting FontSize
                this.FontSize = 12.0 * scale;
                // Prefer high-quality scaling for images at >100% DPI
                RenderOptions.SetBitmapScalingMode(this, scale >= 1.0 ? BitmapScalingMode.HighQuality : BitmapScalingMode.Fant);
                Logger.Information("StudentForm DPI changed: {OldX}->{NewX}", oldDpi.DpiScaleX, newDpi.DpiScaleX);
            }
            catch (System.Exception ex)
            {
                Logger.Warning(ex, "StudentForm: OnDpiChanged handling failed");
            }
        }
    }
}
