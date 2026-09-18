using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Shared;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion editors often mark NumPad keys Handled without inserting, or only accept
/// <see cref="Key.D0"/>–<see cref="Key.D9"/>. UTM/IME can also report NumPad as
/// <see cref="Key.ImeProcessed"/>. Convert NumPad to <see cref="TextComposition"/> once
/// per key event so ZIP, phones, times, and numeric boxes all get the digit.
/// </summary>
public static class NumpadInputHelper
{
    private static bool _registered;
    private static KeyEventArgs? _injectedFor;

    public static void RegisterApplicationWide()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        var handler = new KeyEventHandler(OnPreviewKeyDown);
        EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(SfTextBoxExt), UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(SfMaskedEdit), UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(DoubleTextBox), UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(IntegerTextBox), UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(ComboBoxAdv), UIElement.PreviewKeyDownEvent, handler, handledEventsToo: true);
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e) => HandlePreviewKeyDown(e);

    public static void HandlePreviewKeyDown(KeyEventArgs e)
    {
        if (ReferenceEquals(_injectedFor, e))
        {
            return;
        }

        if (!TryMapNumpadInsert(ResolveEffectiveKey(e), out var insert))
        {
            return;
        }

        if (!TryResolveEditableTextBox(out var textBox, out var host))
        {
            return;
        }

        if (textBox.IsReadOnly || !textBox.IsEnabled)
        {
            return;
        }

        if (host is IntegerTextBox && insert == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
        {
            return;
        }

        _injectedFor = e;

        if (host is DoubleTextBox doubleBox)
        {
            InsertIntoDoubleTextBox(doubleBox, insert == CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator ? "." : insert);
            e.Handled = true;
            return;
        }

        if (host is IntegerTextBox intBox)
        {
            InsertIntoIntegerTextBox(intBox, insert);
            e.Handled = true;
            return;
        }

        InjectText(textBox, insert);
        e.Handled = true;
    }

    internal static Key ResolveEffectiveKey(Key key, Key imeProcessedKey, Key systemKey, Key deadCharProcessedKey)
    {
        if (key == Key.ImeProcessed)
        {
            return imeProcessedKey;
        }

        if (key == Key.System)
        {
            return systemKey;
        }

        if (key == Key.DeadCharProcessed)
        {
            return deadCharProcessedKey;
        }

        return key;
    }

    internal static Key ResolveEffectiveKey(KeyEventArgs e) =>
        ResolveEffectiveKey(e.Key, e.ImeProcessedKey, e.SystemKey, e.DeadCharProcessedKey);

    internal static bool TryMapNumpadInsert(Key key, out string insert)
    {
        insert = string.Empty;
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            insert = ((int)(key - Key.NumPad0)).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (key == Key.Decimal)
        {
            insert = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            return !string.IsNullOrEmpty(insert);
        }

        return false;
    }

    private static void InjectText(TextBox textBox, string insert)
    {
        var before = textBox.Text ?? string.Empty;
        if (InputManager.Current is not null)
        {
            try
            {
                TextCompositionManager.StartComposition(
                    new TextComposition(InputManager.Current, textBox, insert));
            }
            catch
            {
                // Fall through to caret splice.
            }
        }

        if (!string.Equals(textBox.Text, before, StringComparison.Ordinal)
            && (textBox.Text?.Length ?? 0) >= before.Length)
        {
            return;
        }

        var start = textBox.SelectionStart;
        var length = textBox.SelectionLength;
        var next = Splice(before, start, length, insert);
        textBox.Text = next;
        textBox.SelectionStart = Math.Min(next.Length, start + insert.Length);
        textBox.SelectionLength = 0;
    }

    private static bool TryResolveEditableTextBox(out TextBox textBox, out DependencyObject? host)
    {
        textBox = null!;
        host = null;
        var focused = Keyboard.FocusedElement as DependencyObject;
        if (focused is null)
        {
            return false;
        }

        host = FindAncestor<DoubleTextBox>(focused)
            ?? (DependencyObject?)FindAncestor<IntegerTextBox>(focused)
            ?? FindAncestor<SfMaskedEdit>(focused)
            ?? (DependencyObject?)FindAncestor<SfTextBoxExt>(focused)
            ?? FindAncestor<ComboBoxAdv>(focused)
            ?? focused;

        if (focused is TextBox direct)
        {
            textBox = direct;
            return true;
        }

        var inner = FindDescendant<TextBox>(host) ?? FindDescendant<TextBox>(focused);
        if (inner is null)
        {
            return false;
        }

        textBox = inner;
        return true;
    }

    private static void InsertIntoDoubleTextBox(DoubleTextBox box, string insert)
    {
        var current = box.Text;
        if (string.IsNullOrWhiteSpace(current) && box.Value.HasValue)
        {
            current = box.Value.Value.ToString(CultureInfo.CurrentCulture);
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
        if (insert is "." or ",")
        {
            return;
        }

        var current = box.Text;
        if (string.IsNullOrWhiteSpace(current) && box.Value.HasValue)
        {
            current = box.Value.Value.ToString(CultureInfo.CurrentCulture);
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
        if ((insert is "." or ",") && !allowDecimal)
        {
            return;
        }

        var text = getText();
        if (insert is "." or "," && (text.Contains('.') || text.Contains(',')))
        {
            return;
        }

        var next = Splice(text, selectionStart, selectionLength, insert);
        setText(next);
        if (double.TryParse(next, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed)
            || double.TryParse(next, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
        {
            setValue(parsed);
        }

        setSelection(Math.Min(next.Length, selectionStart + insert.Length), 0);
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
