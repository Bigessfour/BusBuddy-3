using System;
using System.Windows.Controls;

namespace BusBuddy.WPF.Services.Navigation
{
    /// <summary>
    /// Provides document-style navigation within the single root Syncfusion DockingManager.
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Ensure a document with the given key is created (if absent) and activated.
        /// </summary>
        void Navigate(string key);

        /// <summary>
        /// Register a pane factory. Idempotent; last registration wins.
        /// </summary>
        void Register(PaneDescriptor descriptor);
    }

    /// <summary>
    /// Describes a pane/document hosted by the DockingManager.
    /// </summary>
    public sealed class PaneDescriptor
    {
        public required string Key { get; init; }
        public required string Header { get; init; }
        public required Func<UserControl> Factory { get; init; }
    }
}
