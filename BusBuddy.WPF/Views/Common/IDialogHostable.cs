using System;

namespace BusBuddy.WPF.Views.Common
{
    /// <summary>
    /// Contract for UserControl-based dialogs hosted in transient Windows.
    /// Provides a uniform close signal and optional disposal hook.
    /// </summary>
    public interface IDialogHostable
    {
        /// <summary>
        /// Raised when the embedded dialog requests its host window to close.
        /// </summary>
        event EventHandler? RequestCloseByHost;

        /// <summary>
        /// Optional resource cleanup invoked by host before window closure.
        /// </summary>
        void DisposeResources();
    }
}
