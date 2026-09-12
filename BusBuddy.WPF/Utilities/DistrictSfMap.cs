using System;
using System.Windows.Input;
using Serilog;
using Syncfusion.UI.Xaml.Maps;

namespace BusBuddy.WPF.Utilities;

/// <summary>
/// SfMap host that swallows Syncfusion mouse-hit NREs when the imagery panel is not
/// in the visual tree yet. VM <c>runtime-errors.log</c> 2026-09-03: <c>SfMap.OnMouseMove</c>
/// null-ref cascade while the clerk hovered the district / pick maps.
/// </summary>
public sealed class DistrictSfMap : SfMap
{
    private static readonly ILogger Logger = Log.ForContext<DistrictSfMap>();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        try
        {
            base.OnMouseMove(e);
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException)
        {
            Logger.Debug(ex, "SfMap.OnMouseMove ignored — imagery layer not ready");
        }
    }
}
