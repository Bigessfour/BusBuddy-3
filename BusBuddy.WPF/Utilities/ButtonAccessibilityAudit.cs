using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;
using Syncfusion.Windows.Tools.Controls;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// Loaded-tree button audit. Not navigation, not ribbon routing, not layout.
/// </summary>
public static class ButtonAccessibilityAudit
{
    public static void Run(DependencyObject root, ILogger logger, string surface)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(logger);
        try
        {
            int total = 0, adv = 0, missingLabel = 0, missingAuto = 0, noCmd = 0;
            foreach (var d in Walk(root))
            {
                if (d is ButtonAdv buttonAdv)
                {
                    total++;
                    adv++;
                    if (buttonAdv.Command is null)
                    {
                        noCmd++;
                    }

                    if (string.IsNullOrWhiteSpace(buttonAdv.Label))
                    {
                        missingLabel++;
                    }

                    if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(buttonAdv)))
                    {
                        missingAuto++;
                    }

                    if (string.IsNullOrWhiteSpace(buttonAdv.Label) &&
                        string.IsNullOrWhiteSpace(AutomationProperties.GetName(buttonAdv)))
                    {
                        logger.Warning(
                            "{Surface} Audit — ButtonAdv missing label and AutomationProperties.Name: {Name}",
                            surface,
                            (buttonAdv as FrameworkElement)?.Name ?? "(unnamed)");
                    }
                }
                else if (d is Button button)
                {
                    total++;
                    if (button.Command is null)
                    {
                        noCmd++;
                    }

                    if (string.IsNullOrWhiteSpace(button.Content?.ToString()))
                    {
                        missingLabel++;
                    }

                    if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
                    {
                        missingAuto++;
                    }

                    if (string.IsNullOrWhiteSpace(button.Content?.ToString()) &&
                        string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
                    {
                        logger.Warning(
                            "{Surface} Audit — Button missing Content and AutomationProperties.Name: {Name}",
                            surface,
                            button.Name ?? "(unnamed)");
                    }
                }
            }

            logger.Information(
                "{Surface} Audit Summary — Buttons={Total}, ButtonAdv={Adv}, MissingLabel/Content={MissingLabel}, MissingAutomationName={MissingAuto}, NoCommand={NoCmd}",
                surface,
                total,
                adv,
                missingLabel,
                missingAuto,
                noCmd);
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "{Surface}: accessibility audit failed", surface);
        }
    }

    private static IEnumerable<DependencyObject> Walk(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Walk(child))
            {
                yield return nested;
            }
        }
    }
}
