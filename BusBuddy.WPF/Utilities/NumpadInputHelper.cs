using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Syncfusion.Windows.Controls.Input;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion SfMaskedEdit and SfTextBoxExt ignore NumPad keys on Windows when focus is on an inner TextBox.
/// </summary>
public static class NumpadInputHelper
{
    public static void HandlePreviewKeyDown(KeyEventArgs e)
    {
        var isNumPadDigit = e.Key is >= Key.NumPad0 and <= Key.NumPad9;
        var isDecimal = e.Key is Key.Decimal or Key.OemPeriod;
        if (!isNumPadDigit && !isDecimal)
        {
            return;
        }

        var insert = isNumPadDigit
            ? ((int)(e.Key - Key.NumPad0)).ToString()
            : ".";

        if (TryGetHost<SfTextBoxExt>(out var textExt))
        {
            InsertIntoTextBox(textExt, insert);
            e.Handled = true;
            return;
        }

        if (TryGetHost<SfMaskedEdit>(out var maskedEdit))
        {
            InsertIntoMaskedEdit(maskedEdit, insert);
            e.Handled = true;
            return;
        }

        if (TryGetHost<TextBox>(out var textBox))
        {
            InsertIntoTextBox(textBox, insert);
            e.Handled = true;
        }
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

    private static void InsertIntoTextBox(SfTextBoxExt textExt, string insert)
    {
        var start = textExt.SelectionStart;
        var len = textExt.SelectionLength;
        var current = textExt.Text ?? string.Empty;
        if (len > 0 && start >= 0 && start + len <= current.Length)
        {
            current = current.Remove(start, len);
        }

        if (start < 0 || start > current.Length)
        {
            start = current.Length;
        }

        textExt.Text = current.Insert(start, insert);
        textExt.SelectionStart = start + insert.Length;
        textExt.SelectionLength = 0;
    }

    private static void InsertIntoTextBox(TextBox textBox, string insert)
    {
        var start = textBox.SelectionStart;
        var len = textBox.SelectionLength;
        var current = textBox.Text ?? string.Empty;
        if (len > 0 && start >= 0 && start + len <= current.Length)
        {
            current = current.Remove(start, len);
        }

        if (start < 0 || start > current.Length)
        {
            start = current.Length;
        }

        textBox.Text = current.Insert(start, insert);
        textBox.SelectionStart = start + insert.Length;
        textBox.SelectionLength = 0;
    }

    private static void InsertIntoMaskedEdit(SfMaskedEdit maskedEdit, string insert)
    {
        var current = maskedEdit.Value?.ToString() ?? string.Empty;
        var start = maskedEdit.SelectionStart;
        var len = maskedEdit.SelectionLength;
        if (len > 0 && start >= 0 && start + len <= current.Length)
        {
            current = current.Remove(start, len);
        }

        if (start < 0 || start > current.Length)
        {
            start = current.Length;
        }

        maskedEdit.Value = current.Insert(start, insert);
        maskedEdit.SelectionStart = start + insert.Length;
        maskedEdit.SelectionLength = 0;
    }
}
