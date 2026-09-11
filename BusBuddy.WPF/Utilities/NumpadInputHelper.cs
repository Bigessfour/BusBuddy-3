using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Shared;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Patches NumPad input only for Syncfusion <see cref="DoubleTextBox"/> / <see cref="IntegerTextBox"/>,
/// which often mark NumPad keys Handled without inserting.
/// Plain <see cref="TextBox"/> / <see cref="SfTextBoxExt"/> accept NumPad natively — do not intercept them
/// (intercepting causes double-insert or fights the caret).
/// </summary>
public static class NumpadInputHelper
{
    private static bool _registered;

    public static void RegisterApplicationWide()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(DoubleTextBox),
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnNumericEditorPreviewKeyDown),
            handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(IntegerTextBox),
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnNumericEditorPreviewKeyDown),
            handledEventsToo: true);
    }

    private static void OnNumericEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        HandlePreviewKeyDown(e);
    }

    public static void HandlePreviewKeyDown(KeyEventArgs e)
    {
        if (!TryGetInsertText(e.Key, out var insert))
        {
            return;
        }

        // Only patch Syncfusion numeric editors. Leave SfTextBoxExt / TextBox alone.
        if (TryGetHost<DoubleTextBox>(out var doubleBox))
        {
            InsertIntoDoubleTextBox(doubleBox, insert);
            e.Handled = true;
            return;
        }

        if (TryGetHost<IntegerTextBox>(out var intBox))
        {
            InsertIntoIntegerTextBox(intBox, insert);
            e.Handled = true;
        }
    }

    private static bool TryGetInsertText(Key key, out string insert)
    {
        insert = string.Empty;
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            insert = ((int)(key - Key.NumPad0)).ToString();
            return true;
        }

        if (key is Key.Decimal or Key.OemPeriod or Key.OemComma)
        {
            insert = ".";
            return true;
        }

        return false;
    }

    private static void InsertIntoDoubleTextBox(DoubleTextBox box, string insert)
    {
        var current = box.Text;
        if (string.IsNullOrWhiteSpace(current) && box.Value.HasValue)
        {
            current = box.Value.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        current ??= string.Empty;
        var (start, length) = GetSelection(box);
        InsertIntoNumericEditorText(
            () => current,
            t =>
            {
                box.Text = t;
                var inner = FindDescendant<TextBox>(box);
                if (inner is not null)
                {
                    inner.Text = t;
                }
            },
            (s, len) => SetSelection(box, s, len),
            start,
            length,
            insert,
            allowDecimal: true,
            setValue: parsed =>
            {
                var min = box.MinValue;
                var max = box.MaxValue;
                if (parsed < min)
                {
                    parsed = min;
                }

                if (parsed > max)
                {
                    parsed = max;
                }

                box.Value = parsed;
            });
    }

    private static void InsertIntoIntegerTextBox(IntegerTextBox box, string insert)
    {
        if (insert == ".")
        {
            return;
        }

        var current = box.Text;
        if (string.IsNullOrWhiteSpace(current) && box.Value.HasValue)
        {
            current = box.Value.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        current ??= string.Empty;
        var (start, length) = GetSelection(box);
        InsertIntoNumericEditorText(
            () => current,
            t =>
            {
                box.Text = t;
                var inner = FindDescendant<TextBox>(box);
                if (inner is not null)
                {
                    inner.Text = t;
                }
            },
            (s, len) => SetSelection(box, s, len),
            start,
            length,
            insert,
            allowDecimal: false,
            setValue: parsed =>
            {
                var asInt = (int)Math.Round(parsed, MidpointRounding.AwayFromZero);
                var min = (int)box.MinValue;
                var max = (int)box.MaxValue;
                if (asInt < min)
                {
                    asInt = min;
                }

                if (asInt > max)
                {
                    asInt = max;
                }

                box.Value = asInt;
            });
    }

    private static (int Start, int Length) GetSelection(DependencyObject box)
    {
        if (Keyboard.FocusedElement is TextBox tb)
        {
            return (tb.SelectionStart, tb.SelectionLength);
        }

        if (box is DoubleTextBox d)
        {
            return (d.SelectionStart, d.SelectionLength);
        }

        if (box is IntegerTextBox i)
        {
            return (i.SelectionStart, i.SelectionLength);
        }

        return (0, 0);
    }

    private static void SetSelection(DependencyObject box, int start, int length)
    {
        try
        {
            if (box is DoubleTextBox d)
            {
                d.SelectionStart = start;
                d.SelectionLength = length;
            }
            else if (box is IntegerTextBox i)
            {
                i.SelectionStart = start;
                i.SelectionLength = length;
            }
        }
        catch
        {
            /* template may not be ready */
        }

        var inner = FindDescendant<TextBox>(box);
        if (inner is not null)
        {
            inner.SelectionStart = Math.Min(start, inner.Text?.Length ?? 0);
            inner.SelectionLength = length;
        }
    }

    private static void InsertIntoNumericEditorText(
        Func<string> getText,
        Action<string> setText,
        Action<int, int> setSelection,
        int selectionStart,
        int selectionLength,
        string insert,
        bool allowDecimal,
        Action<double> setValue)
    {
        if (insert == "." && !allowDecimal)
        {
            return;
        }

        var text = getText();
        if (insert == "." && text.Contains('.'))
        {
            return;
        }

        var next = Splice(text, selectionStart, selectionLength, insert);
        setText(next);
        if (double.TryParse(next, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out var parsed)
            || double.TryParse(next, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsed))
        {
            setValue(parsed);
        }

        setSelection(Math.Min(next.Length, selectionStart + insert.Length), 0);
    }

    private static bool TryGetHost<T>(out T host) where T : DependencyObject
    {
        host = default!;
        if (Keyboard.FocusedElement is T direct)
        {
            host = direct;
            return true;
        }

        if (Keyboard.FocusedElement is DependencyObject focused)
        {
            var ancestor = FindAncestor<T>(focused);
            if (ancestor is not null)
            {
                host = ancestor;
                return true;
            }
        }

        return false;
    }

    private static T? FindAncestor<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
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

    private static string Splice(string current, int start, int len, string insert)
    {
        if (len > 0 && start >= 0 && start + len <= current.Length)
        {
            current = current.Remove(start, len);
        }

        if (start < 0 || start > current.Length)
        {
            start = current.Length;
        }

        return current.Insert(start, insert);
    }
}
