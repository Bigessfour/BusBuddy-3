using System.Linq;
using System.Windows;

namespace BusBuddy.WPF.Utilities;

/// <summary>Sets Owner on a modal dialog from the active window. Owner is optional.</summary>
internal static class DialogOwner
{
    public static void Assign(Window dialog)
    {
        try
        {
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                ?? Application.Current?.MainWindow;
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
}
