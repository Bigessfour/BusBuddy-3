using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using BusBuddy.WPF.Views.Bus;
using Serilog;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Non-blocking toast notifications for clerk feedback (save rules, delete/soft-retire outcomes).
/// Reuses <see cref="NotificationWindow"/> styling; auto-dismisses so it does not trap the UI like MessageBox.
/// </summary>
public static class UserToast
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UserToast));

    public static void Success(string message, string title = "Success") =>
        Show(message, title, NotificationWindow.NotificationType.Success);

    public static void Error(string message, string title = "Error") =>
        Show(message, title, NotificationWindow.NotificationType.Error, autoCloseMs: 6000);

    public static void Warning(string message, string title = "Notice") =>
        Show(message, title, NotificationWindow.NotificationType.Warning, autoCloseMs: 5000);

    public static void Info(string message, string title = "Info") =>
        Show(message, title, NotificationWindow.NotificationType.Information);

    public static void Show(
        string message,
        string title,
        NotificationWindow.NotificationType type,
        int autoCloseMs = 4000)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
            {
                ShowCore(message, title, type, autoCloseMs);
            }
            else
            {
                dispatcher.Invoke(() => ShowCore(message, title, type, autoCloseMs));
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "UserToast failed — falling back to status only. Title={Title} Message={Message}", title, message);
        }
    }

    private static void ShowCore(
        string message,
        string title,
        NotificationWindow.NotificationType type,
        int autoCloseMs)
    {
        var toast = new NotificationWindow(message, title, type)
        {
            Width = 420,
            Height = 180,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = true,
            ShowInTaskbar = false
        };

        try
        {
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? Application.Current?.MainWindow;
            if (owner is { IsLoaded: true })
            {
                toast.Owner = owner;
            }
        }
        catch
        {
            // Owner is optional for toast.
        }

        PositionBottomRight(toast);

        toast.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(1500, autoCloseMs)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                if (toast.IsVisible)
                {
                    toast.Close();
                }
            }
            catch
            {
                // Already closed.
            }
        };
        timer.Start();

        Logger.Information("Toast {Type}: {Title} — {Message}", type, title, message);
    }

    private static void PositionBottomRight(Window toast)
    {
        try
        {
            var work = SystemParameters.WorkArea;
            toast.Left = work.Right - toast.Width - 24;
            toast.Top = work.Bottom - toast.Height - 24;
        }
        catch
        {
            toast.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
}
