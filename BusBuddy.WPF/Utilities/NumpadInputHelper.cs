using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Syncfusion.Windows.Controls.Input;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Syncfusion SfTextBoxExt, SfMaskedEdit, and editable ComboBoxAdv host an inner <see cref="TextBox"/>
/// that ignores NumPad keys on Windows. This helper injects digits into the caret host.
/// Register once at app startup via <see cref="RegisterApplicationWide"/>.
/// </summary>
public static class NumpadInputHelper
{
    private static bool _registered;

    /// <summary>Attach a tunneling PreviewKeyDown handler to all <see cref="Window"/> instances.</summary>
    public static void RegisterApplicationWide()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(Window),
            UIElement.PreviewKeyDownEvent,
            new KeyEventHandler(OnWindowPreviewKeyDown),
            handledEventsToo: true);
    }

    private static void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        HandlePreviewKeyDown(e);
    }

    public static void HandlePreviewKeyDown(KeyEventArgs e)
    {
        if (!TryGetInsertText(e.Key, out var insert))
        {
            return;
        }

        if (TryInsertIntoFocusedTextBox(insert))
        {
            e.Handled = true;
            return;
        }

        if (TryGetHost<SfTextBoxExt>(out var textExt))
        {
            InsertIntoSfTextBoxExt(textExt, insert);
            e.Handled = true;
            return;
        }

        if (TryGetHost<SfMaskedEdit>(out var maskedEdit))
        {
            InsertIntoMaskedEdit(maskedEdit, insert);
            e.Handled = true;
            return;
        }

        if (TryGetHost<ComboBoxAdv>(out var combo) && combo.IsEditable)
        {
            if (combo.Template?.FindName("PART_EditableTextBox", combo) is TextBox comboEditor)
            {
                InsertIntoTextBox(comboEditor, insert);
                e.Handled = true;
            }
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

        if (key is Key.Decimal or Key.OemPeriod)
        {
            insert = ".";
            return true;
        }

        return false;
    }

    private static bool TryInsertIntoFocusedTextBox(string insert)
    {
        if (Keyboard.FocusedElement is TextBox focusedTextBox)
        {
            InsertIntoTextBox(focusedTextBox, insert);
            return true;
        }

        if (Keyboard.FocusedElement is DependencyObject focused)
        {
            var inner = FindDescendant<TextBox>(focused);
            if (inner is not null)
            {
                InsertIntoTextBox(inner, insert);
                return true;
            }
        }

        return false;
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

    private static void InsertIntoSfTextBoxExt(SfTextBoxExt textExt, string insert)
    {
        if (textExt.Template?.FindName("PART_TextBox", textExt) is TextBox inner)
        {
            InsertIntoTextBox(inner, insert);
            return;
        }

        var start = textExt.SelectionStart;
        var len = textExt.SelectionLength;
        var current = textExt.Text ?? string.Empty;
        textExt.Text = Splice(current, start, len, insert);
        textExt.SelectionStart = Math.Min(textExt.Text?.Length ?? 0, start + insert.Length);
        textExt.SelectionLength = 0;
    }

    private static void InsertIntoTextBox(TextBox textBox, string insert)
    {
        var start = textBox.SelectionStart;
        var len = textBox.SelectionLength;
        var current = textBox.Text ?? string.Empty;
        textBox.Text = Splice(current, start, len, insert);
        textBox.SelectionStart = Math.Min(textBox.Text?.Length ?? 0, start + insert.Length);
        textBox.SelectionLength = 0;
    }

    private static void InsertIntoMaskedEdit(SfMaskedEdit maskedEdit, string insert)
    {
        if (maskedEdit.Template?.FindName("PART_TextBox", maskedEdit) is TextBox inner)
        {
            InsertIntoTextBox(inner, insert);
            return;
        }

        var current = maskedEdit.Value?.ToString() ?? string.Empty;
        var start = maskedEdit.SelectionStart;
        var len = maskedEdit.SelectionLength;
        maskedEdit.Value = Splice(current, start, len, insert);
        maskedEdit.SelectionStart = Math.Min(maskedEdit.Value?.ToString()?.Length ?? 0, start + insert.Length);
        maskedEdit.SelectionLength = 0;
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
