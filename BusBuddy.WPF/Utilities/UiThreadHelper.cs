using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace BusBuddy.WPF.Utilities
{
    /// <summary>
    /// Utility class for UI thread management to improve application responsiveness
    /// </summary>
    public static class UiThreadHelper
    {
        /// <summary>
        /// Runs a task on a background thread while keeping UI responsive
        /// </summary>
        /// <param name="backgroundTask">The task to run in the background</param>
        /// <param name="priority">Priority for the background operation</param>
        /// <returns>A task representing the background operation</returns>
        public static async Task RunOnBackgroundThreadAsync(Func<Task> backgroundTask, DispatcherPriority priority = DispatcherPriority.Background)
        {
            // Create a TaskCompletionSource to represent the background work
            var taskCompletionSource = new TaskCompletionSource<bool>();

            // Get the UI dispatcher
            var dispatcher = Application.Current.Dispatcher;

            // Run the work on a background thread
            await Task.Run(async () =>
            {
                try
                {
                    // Execute the actual background work
                    await backgroundTask();

                    // Signal completion on the UI thread to avoid thread safety issues
                    // Using Invoke instead of BeginInvoke to wait for completion
                    dispatcher.Invoke(priority, new Action(() =>
                    {
                        taskCompletionSource.SetResult(true);
                    }));
                }
                catch (Exception ex)
                {
                    // If an error occurs, propagate it through the task
                    // Using Invoke instead of BeginInvoke to wait for completion
                    dispatcher.Invoke(priority, new Action(() =>
                    {
                        taskCompletionSource.SetException(ex);
                    }));
                }
            });

            // Wait for the background work to complete
            await taskCompletionSource.Task;
        }

        /// <summary>
        /// Yields control back to the UI thread for a specified duration to improve responsiveness
        /// </summary>
        /// <param name="millisecondsDelay">Optional delay in milliseconds</param>
        public static async Task YieldToUiAsync(int millisecondsDelay = 10)
        {
            // Yield execution and then return after a small delay
            await Task.Delay(millisecondsDelay);
        }

        /// <summary>
        /// Runs a low-priority action on the UI thread after yielding to higher priority work
        /// </summary>
        /// <param name="action">The action to run</param>
        public static async Task RunLowPriorityAsync(Action action)
        {
            // Yield to let higher priority work complete
            await Task.Yield();

            // Get the UI dispatcher
            var dispatcher = Application.Current.Dispatcher;

            // Using InvokeAsync instead of BeginInvoke for proper awaiting
            var dispatcherOperation = dispatcher.InvokeAsync(action, DispatcherPriority.ApplicationIdle);
            await dispatcherOperation.Task;
        }
    }
}
