using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Windows.Controls;
using Syncfusion.Windows.Tools.Controls; // Documented DockingManager API

namespace BusBuddy.WPF.Services.Navigation
{
    /// <summary>
    /// Manages creation/activation of document panes inside the root DockingManager using documented Syncfusion APIs.
    /// </summary>
    public sealed class NavigationService : INavigationService
    {
        private readonly DockingManager _dockingManager;
        private readonly ConcurrentDictionary<string, PaneDescriptor> _registry = new();

        public NavigationService(DockingManager dockingManager)
        {
            _dockingManager = dockingManager ?? throw new ArgumentNullException(nameof(dockingManager));
        }

        public void Register(PaneDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            _registry[descriptor.Key] = descriptor; // upsert
        }

        public void Navigate(string key)
        {
            if (!_registry.TryGetValue(key, out var desc)) return; // unknown key => no-op

            var existing = _dockingManager.Children
                .OfType<ContentControl>()
                .FirstOrDefault(c => Equals(DockingManager.GetHeader(c), desc.Header));

            if (existing != null)
            {
                try { _dockingManager.ActivateWindow(desc.Header); } catch { }
                return;
            }

            var container = new ContentControl { Content = desc.Factory() };
            DockingManager.SetHeader(container, desc.Header);
            DockingManager.SetState(container, DockState.Document);
            _dockingManager.Children.Add(container);
            try { _dockingManager.ActivateWindow(desc.Header); } catch { }
        }
    }
}
