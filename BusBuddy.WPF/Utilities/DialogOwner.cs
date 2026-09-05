using System.Windows;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Nested dialogs (Students, Route Management) disable MainWindow.
/// Child windows must be owned by the active window or they stay behind the modal.
/// </summary>
internal static class DialogOwner
{
    public static void Assign(Window dialog)
    {
        try
        {
            var owner = Resolve(null);
            if (owner != null && !ReferenceEquals(owner, dialog))
            {
                dialog.Owner = owner;
            }
        }
        catch
        {
            // Modal dialogs still work without Owner.
        }
    }

    public static Window? Resolve(Window? requested, Window? exclude = null)
    {
        var app = Application.Current;
        if (app?.Windows is null)
        {
            return requested;
        }

        Window? active = null;
        foreach (Window window in app.Windows)
        {
            if (window is { IsLoaded: true, IsVisible: true, IsActive: true } &&
                !ReferenceEquals(window, exclude))
            {
                active = window;
                break;
            }
        }

        return active ?? requested ?? app.MainWindow;
    }
}
