using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace BusBuddy.WPF.Logging;

/// <summary>
/// Routes WPF PresentationTraceSources (bindings, markup, resource dictionaries)
/// into Serilog so a clerk click-through captures misconfigured elements.
/// Binding errors previously went only to <c>BusBuddy.WPF.XamlDiagnostics.log</c>.
/// </summary>
public static class WpfTraceSerilogListener
{
    public const string BindingErrorMarker = "BindingExpression path error";
    public const string CannotFindSourceMarker = "Cannot find source for binding";
    public const string ResourceNotFoundMarker = "cannot be found";

    private static readonly ILogger Logger = Log.ForContext(typeof(WpfTraceSerilogListener));
    private static readonly ConcurrentDictionary<string, int> RepeatCounts = new(StringComparer.Ordinal);
    private static SerilogTraceListener? _listener;
    private static bool _attached;

    /// <summary>True when the message is a binding/resource/markup failure worth logging.</summary>
    public static bool IsMisconfigurationTrace(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (IsVendorAnimationTrace(message))
        {
            return false;
        }

        if (message.Contains(BindingErrorMarker, StringComparison.OrdinalIgnoreCase)
            || message.Contains(CannotFindSourceMarker, StringComparison.OrdinalIgnoreCase)
            || message.Contains("BindingExpression", StringComparison.OrdinalIgnoreCase)
                && message.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (message.Contains("StaticResource", StringComparison.OrdinalIgnoreCase)
            && message.Contains(ResourceNotFoundMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (message.Contains("XamlParseException", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Provide value on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Syncfusion ComboBoxAdv popup animations bind ElementName=PanelPresenter / ItemsPresenter
    /// before the template namescope exists (VM 2026-09-17 Activity Timeline flood).
    /// </summary>
    internal static bool IsVendorAnimationTrace(string message)
    {
        if (!message.Contains("DoubleAnimation", StringComparison.OrdinalIgnoreCase)
            || !message.Contains("Duration", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return message.Contains("ElementName=PanelPresenter", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ElementName=ItemsPresenter", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Attach once. Safe to call before MainWindow exists.</summary>
    public static void Attach()
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _listener = new SerilogTraceListener();

        PresentationTraceSources.Refresh();
        AttachSource(PresentationTraceSources.DataBindingSource, SourceLevels.Warning);
        AttachSource(PresentationTraceSources.MarkupSource, SourceLevels.Warning);
        AttachSource(PresentationTraceSources.ResourceDictionarySource, SourceLevels.Warning);
        SuppressVendorAnimationListeners(PresentationTraceSources.AnimationSource);

        UiDiagnosticsLog.Write(
            Logger,
            LogEventLevel.Information,
            "WPF presentation traces attached — binding/markup/resource warnings go to Serilog and {File}",
            UiDiagnosticsLog.ResolveLogPath());
    }

    private static void AttachSource(TraceSource source, SourceLevels minimum)
    {
        if (source.Switch.Level < minimum)
        {
            source.Switch.Level = minimum;
        }

        if (_listener is not null && !source.Listeners.Contains(_listener))
        {
            source.Listeners.Add(_listener);
        }

        SuppressVendorAnimationListeners(source);
    }

    /// <summary>
    /// app.config TextWriterTraceListener still receives PresentationTrace warnings. ComboBoxAdv
    /// Fluent templates flood XamlDiagnostics.log and stall first open unless listeners skip them.
    /// </summary>
    internal static void SuppressVendorAnimationListeners(TraceSource source)
    {
        foreach (TraceListener listener in source.Listeners)
        {
            if (listener.Filter is VendorAnimationTraceFilter)
            {
                continue;
            }

            listener.Filter = new VendorAnimationTraceFilter(listener.Filter);
        }
    }

    internal static void HandleTrace(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var trimmed = message.Trim();
        if (!IsMisconfigurationTrace(trimmed))
        {
            return;
        }

        var count = RepeatCounts.AddOrUpdate(trimmed, 1, (_, n) => n + 1);
        if (count > 1 && count % 25 != 0)
        {
            return;
        }

        var suffix = count == 1 ? string.Empty : $" (repeat {count})";
        UiDiagnosticsLog.Write(
            Logger,
            LogEventLevel.Warning,
            "WPF misconfiguration{Repeat}: {Trace}",
            suffix,
            trimmed);
    }

    internal sealed class VendorAnimationTraceFilter : TraceFilter
    {
        private readonly TraceFilter? _inner;

        public VendorAnimationTraceFilter(TraceFilter? inner)
        {
            _inner = inner;
        }

        public override bool ShouldTrace(
            TraceEventCache? cache,
            string source,
            TraceEventType eventType,
            int id,
            string? formatOrMessage,
            object?[]? args,
            object? data1,
            object?[]? data)
        {
            var message = formatOrMessage;
            if (string.IsNullOrEmpty(message) && data1 is string dataMessage)
            {
                message = dataMessage;
            }

            if (!string.IsNullOrEmpty(message) && IsVendorAnimationTrace(message))
            {
                return false;
            }

            return _inner?.ShouldTrace(cache, source, eventType, id, formatOrMessage, args, data1, data) ?? true;
        }
    }

    private sealed class SerilogTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            HandleTrace(message);
        }

        public override void WriteLine(string? message)
        {
            HandleTrace(message);
        }
    }
}
