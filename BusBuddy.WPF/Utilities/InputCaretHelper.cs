using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Aligns Syncfusion inner <see cref="TextBox"/> hosts and drives a visible blinking caret
/// so clerks can see insertion position in empty fields. Register once at app startup.
/// </summary>
public static class InputCaretHelper
{
    private static bool _registered;
    private static readonly ConcurrentDictionary<TextBox, CaretBlinkController> ActiveBlinks = new();

    public static void RegisterApplicationWide()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        EventManager.RegisterClassHandler(
            typeof(SfTextBoxExt),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSyncfusionInputLoaded));
        EventManager.RegisterClassHandler(
            typeof(SfMaskedEdit),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSyncfusionInputLoaded));
        EventManager.RegisterClassHandler(
            typeof(ComboBoxAdv),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnSyncfusionInputLoaded));
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnPlainTextBoxLoaded));

        EventManager.RegisterClassHandler(
            typeof(UIElement),
            UIElement.GotKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(UIElement),
            UIElement.LostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus),
            handledEventsToo: true);
    }

    private static void OnSyncfusionInputLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement host)
        {
            return;
        }

        ApplyInnerTextBoxAlignment(host);

        if (sender is SfTextBoxExt textExt)
        {
            textExt.CaretBrush = ResolveCaretBrush();
        }
        else if (sender is SfMaskedEdit maskedEdit)
        {
            maskedEdit.CaretBrush = ResolveCaretBrush();
        }
    }

    private static void OnPlainTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || IsSyncfusionInnerHost(textBox))
        {
            return;
        }

        textBox.VerticalContentAlignment = VerticalAlignment.Center;
        textBox.HorizontalContentAlignment = HorizontalAlignment.Left;
        textBox.CaretBrush = ResolveCaretBrush();
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is not DependencyObject focused)
        {
            return;
        }

        var inner = ResolveEditableTextBox(focused);
        if (inner is not null)
        {
            StartBlink(inner);
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OldFocus is not DependencyObject oldFocus)
        {
            return;
        }

        var inner = ResolveEditableTextBox(oldFocus);
        if (inner is not null)
        {
            StopBlink(inner);
        }
    }

    private static void ApplyInnerTextBoxAlignment(FrameworkElement host)
    {
        var inner = ResolveInnerTextBox(host);
        if (inner is null)
        {
            return;
        }

        inner.VerticalContentAlignment = VerticalAlignment.Center;
        inner.HorizontalContentAlignment = HorizontalAlignment.Left;
        inner.TextAlignment = TextAlignment.Left;
        inner.BorderThickness = new Thickness(0);
        inner.Background = Brushes.Transparent;
        inner.Padding = new Thickness(0);
        inner.Margin = new Thickness(0);
        inner.SnapsToDevicePixels = true;
        inner.SetValue(FrameworkElement.UseLayoutRoundingProperty, true);
        inner.CaretBrush = ResolveCaretBrush();
    }

    private static TextBox? ResolveEditableTextBox(DependencyObject focused)
    {
        if (focused is TextBox textBox)
        {
            return textBox;
        }

        if (focused is FrameworkElement host)
        {
            return ResolveInnerTextBox(host) ?? FindDescendant<TextBox>(host);
        }

        return FindDescendant<TextBox>(focused);
    }

    private static TextBox? ResolveInnerTextBox(FrameworkElement host)
    {
        if (host is SfTextBoxExt textExt)
        {
            return textExt.Template?.FindName("PART_TextBox", textExt) as TextBox;
        }

        if (host is SfMaskedEdit maskedEdit)
        {
            return maskedEdit.Template?.FindName("PART_TextBox", maskedEdit) as TextBox;
        }

        if (host is ComboBoxAdv combo && combo.IsEditable)
        {
            return combo.Template?.FindName("PART_EditableTextBox", combo) as TextBox;
        }

        return null;
    }

    private static bool IsSyncfusionInnerHost(TextBox textBox)
    {
        var parent = VisualTreeHelper.GetParent(textBox);
        while (parent is not null)
        {
            if (parent is SfTextBoxExt or SfMaskedEdit or ComboBoxAdv)
            {
                return true;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return false;
    }

    private static void StartBlink(TextBox textBox)
    {
        StopBlink(textBox);
        var controller = new CaretBlinkController(textBox, ResolveCaretBrush());
        if (ActiveBlinks.TryAdd(textBox, controller))
        {
            controller.Start();
        }
    }

    private static void StopBlink(TextBox textBox)
    {
        if (ActiveBlinks.TryRemove(textBox, out var controller))
        {
            controller.Stop();
        }
    }

    private static Brush ResolveCaretBrush()
    {
        try
        {
            if (Application.Current?.TryFindResource("BusBuddy.Brush.Text.Accent") is Brush accent)
            {
                return accent;
            }

            if (Application.Current?.TryFindResource("FormInputForegroundBrush") is Brush foreground)
            {
                return foreground;
            }
        }
        catch
        {
            // Theme not ready yet.
        }

        return Brushes.White;
    }

    private static T? FindDescendant<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private sealed class CaretBlinkController
    {
        private readonly TextBox _textBox;
        private readonly Brush _visibleBrush;
        private readonly Brush _hiddenBrush = Brushes.Transparent;
        private readonly DispatcherTimer _timer;
        private bool _showCaret = true;

        public CaretBlinkController(TextBox textBox, Brush visibleBrush)
        {
            _textBox = textBox;
            _visibleBrush = visibleBrush;
            var interval = TimeSpan.FromMilliseconds(530);

            _timer = new DispatcherTimer
            {
                Interval = interval,
            };
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            _showCaret = true;
            _textBox.CaretBrush = _visibleBrush;
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
            _textBox.CaretBrush = _visibleBrush;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _showCaret = !_showCaret;
            _textBox.CaretBrush = _showCaret ? _visibleBrush : _hiddenBrush;
        }
    }
}
