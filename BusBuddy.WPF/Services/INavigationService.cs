using System;
using System.Collections.Generic;

namespace BusBuddy.WPF.Services
{
    /// <summary>
    /// Centralized navigation service for BusBuddy WPF application
    /// Provides clean, testable navigation abstraction
    /// </summary>
    public interface INavigationService
    {
        /// <summary>
        /// Current active view model
        /// </summary>
        object? CurrentViewModel { get; }

        /// <summary>
        /// Current view title for display
        /// </summary>
        string CurrentViewTitle { get; }

        /// <summary>
        /// Navigation history for back functionality
        /// </summary>
        IReadOnlyList<string> NavigationHistory { get; }

        /// <summary>
        /// Navigate to a specific view by name
        /// </summary>
        /// <param name="viewName">Name of the view to navigate to</param>
        void NavigateTo(string viewName);

        /// <summary>
        /// Navigate to a specific view with parameters
        /// </summary>
        /// <param name="viewName">Name of the view to navigate to</param>
        /// <param name="parameters">Navigation parameters</param>
        void NavigateTo(string viewName, object? parameters);

        /// <summary>
        /// Navigate back to previous view
        /// </summary>
        /// <returns>True if navigation was successful, false if no history</returns>
        bool NavigateBack();

        /// <summary>
        /// Check if we can navigate back
        /// </summary>
        bool CanNavigateBack { get; }

        /// <summary>
        /// Clear navigation history
        /// </summary>
        void ClearHistory();

        /// <summary>
        /// Event fired when navigation occurs
        /// </summary>
        event EventHandler<NavigationEventArgs>? NavigationChanged;
    }

    /// <summary>
    /// Event arguments for navigation events
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        public string ViewName { get; }
        public object? Parameters { get; }
        public object? ViewModel { get; }
        public string ViewTitle { get; }

        public NavigationEventArgs(string viewName, object? parameters, object? viewModel, string viewTitle)
        {
            ViewName = viewName;
            Parameters = parameters;
            ViewModel = viewModel;
            ViewTitle = viewTitle;
        }
    }
}
