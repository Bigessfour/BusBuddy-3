using System;
using System.Windows;
using Serilog;
using Serilog.Events;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Logging;

/// <summary>
/// Greppable UI-proof lines for clerk hops. Dual-writes to Serilog and
/// <c>logs/ui-diagnostics-.log</c>. Search the file for <c>UI proof Click=</c>.
/// </summary>
internal static class UiProofLog
{
    internal const string Template = "UI proof Click={Click} Surface={Surface} Outcome={Outcome} Detail={Detail}";

    internal static string Label(object? sender, string fallback)
    {
        if (sender is ButtonAdv button && !string.IsNullOrWhiteSpace(button.Label))
        {
            return button.Label;
        }

        if (sender is FrameworkElement element && !string.IsNullOrWhiteSpace(element.Name))
        {
            return element.Name;
        }

        return fallback;
    }

    internal static void Write(ILogger logger, string click, string surface, string outcome, string? detail = null)
    {
        UiDiagnosticsLog.Write(
            logger,
            LogEventLevel.Information,
            Template,
            click,
            surface,
            outcome,
            detail ?? string.Empty);
    }

    internal static void Failed(ILogger logger, Exception exception, string click, string surface)
    {
        UiDiagnosticsLog.Write(
            logger,
            LogEventLevel.Error,
            exception,
            Template,
            click,
            surface,
            "failed",
            exception.Message);
    }
}
