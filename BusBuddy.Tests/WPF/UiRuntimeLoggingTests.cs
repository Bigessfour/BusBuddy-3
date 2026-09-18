using System;
using BusBuddy.WPF.Logging;
using BusBuddy.WPF.Views.Settings;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class UiRuntimeLoggingTests
{
    [Test]
    public void BindingTrace_DetectsPathAndSourceErrors()
    {
        Assert.That(
            WpfTraceSerilogListener.IsMisconfigurationTrace(
                "System.Windows.Data Error: 40 : BindingExpression path error: 'Foo' property not found on 'object' ''StudentsViewModel'"),
            Is.True);
        Assert.That(
            WpfTraceSerilogListener.IsMisconfigurationTrace(
                "Cannot find source for binding with reference 'RelativeSource FindAncestor'"),
            Is.True);
        Assert.That(
            WpfTraceSerilogListener.IsMisconfigurationTrace(
                "StaticResource resource 'MissingBrush' cannot be found"),
            Is.True);
        Assert.That(WpfTraceSerilogListener.IsMisconfigurationTrace("Loaded Visual from template"), Is.False);
        Assert.That(WpfTraceSerilogListener.IsMisconfigurationTrace(null), Is.False);
        Assert.That(
            WpfTraceSerilogListener.IsMisconfigurationTrace(
                "Cannot find source for binding with reference 'ElementName=PanelPresenter'. BindingExpression:Path=SafeHeight; DataItem=null; target element is 'DoubleAnimation' (HashCode=1); target property is 'Duration' (type 'Duration')"),
            Is.False);
        Assert.That(
            WpfTraceSerilogListener.IsMisconfigurationTrace(
                "Cannot find source for binding with reference 'ElementName=ItemsPresenter'. BindingExpression:Path=DesiredSize.Height; DataItem=null; target element is 'DoubleAnimation'; target property is 'Duration'"),
            Is.False);
    }

    [Test]
    public void CapabilityPresence_TreatsPlaceholdersAsMissing()
    {
        Assert.That(RuntimeCapabilityLogger.DescribePresence("${GOOGLE_MAPS_API_KEY}"), Is.EqualTo("missing"));
        Assert.That(RuntimeCapabilityLogger.DescribePresence("   "), Is.EqualTo("missing"));
        Assert.That(RuntimeCapabilityLogger.DescribePresence(null), Is.EqualTo("missing"));
        Assert.That(RuntimeCapabilityLogger.DescribePresence("present-key"), Is.EqualTo("present"));
    }

    [Test]
    public void SurfaceProbe_ClassifiesBusBuddyViewsAndNullDataContext()
    {
        Assert.That(UiSurfaceProbe.IsBusBuddySurface(typeof(SettingsView)), Is.True);
        Assert.That(UiSurfaceProbe.IsBusBuddySurface(typeof(string)), Is.False);
        Assert.That(UiSurfaceProbe.DescribeDataContext(null), Is.EqualTo("(null)"));
        Assert.That(UiSurfaceProbe.DescribeDataContext(new object()), Does.Contain("Object"));
        Assert.That(
            UiSurfaceProbe.ClassifyDataContextLevel(null, descendantHasDataContext: false, isWindow: false),
            Is.EqualTo(Serilog.Events.LogEventLevel.Warning));
        Assert.That(
            UiSurfaceProbe.ClassifyDataContextLevel(null, descendantHasDataContext: true, isWindow: false),
            Is.EqualTo(Serilog.Events.LogEventLevel.Information));
        Assert.That(
            UiSurfaceProbe.ClassifyDataContextLevel(null, descendantHasDataContext: false, isWindow: true),
            Is.EqualTo(Serilog.Events.LogEventLevel.Information));
        Assert.That(
            UiSurfaceProbe.ClassifyDataContextLevel(new object(), descendantHasDataContext: false, isWindow: false),
            Is.EqualTo(Serilog.Events.LogEventLevel.Information));
        Assert.That(UiSurfaceProbe.IsEmbeddedControl(typeof(BusBuddy.WPF.Controls.PlacesAddressBox)), Is.True);
        Assert.That(UiSurfaceProbe.IsEmbeddedControl(typeof(SettingsView)), Is.False);
        Assert.That(
            UiSurfaceProbe.ClassifyDataContextLevel(
                null,
                descendantHasDataContext: false,
                isWindow: false,
                isEmbeddedControl: true),
            Is.EqualTo(Serilog.Events.LogEventLevel.Information));
    }

    [Test]
    public void AppStartup_WiresUiDiagnosticsAndCapabilitySnapshot()
    {
        var source = XamlViewFile.Read("App.xaml.cs") + "\n" + XamlViewFile.Read("App.Exceptions.cs");
        Assert.That(source, Does.Contain("WpfTraceSerilogListener.Attach"));
        Assert.That(source, Does.Contain("UiSurfaceProbe.Register"));
        Assert.That(source, Does.Contain("RuntimeCapabilityLogger.WriteStartupSnapshot"));
        Assert.That(source, Does.Contain("LoggingModeManager.Initialize"));
        Assert.That(source, Does.Contain("OnUnobservedTaskException"));
        Assert.That(source, Does.Contain("IsLayoutTransientException"));
        Assert.That(source, Does.Contain("IsRepeatedUiError"));
        Assert.That(source, Does.Contain("_layoutTransientCount"));
        Assert.That(source, Does.Contain("Layout transient swallowed"));
        Assert.That(
            source.IndexOf("IsLayoutTransientException(e.Exception)", StringComparison.Ordinal),
            Is.LessThan(source.IndexOf("AppendAllText(runtimeErrorsPath", StringComparison.Ordinal)));
        var tileLayer = XamlViewFile.Read("Utilities/GoogleMapTilesImageryLayer.cs");
        Assert.That(tileLayer, Does.Contain("MeasureOverride"));
        Assert.That(tileLayer, Does.Contain("ArrangeOverride"));
        Assert.That(tileLayer, Does.Contain("OnPropertyChanged"));
        Assert.That(tileLayer, Does.Contain("IsVisualTreeNotReady"));
        Assert.That(tileLayer, Does.Contain("BeginMarkerHostCheck"));
        Assert.That(tileLayer, Does.Contain("LastLayoutSkippedVisualTree"));
        var host = XamlViewFile.Read("Utilities/MapMarkerHost.cs");
        Assert.That(host, Does.Contain("TryAssign"));
        Assert.That(host, Does.Contain("TryAssignAndLayout"));
        Assert.That(host, Does.Contain("RetryScheduler"));
        var mapView = XamlViewFile.Read("Views/Map/MapView.xaml.cs");
        Assert.That(mapView, Does.Contain("Map marker host retry paused"));
        Assert.That(mapView, Does.Not.Contain("Map markers still pending after"));
        var ollama = CoreSourceFile.Read("Services/OllamaAiService.cs");
        Assert.That(ollama, Does.Contain("Logger.Information(ex,"));
        Assert.That(ollama, Does.Contain("Ollama is not reachable"));
    }

    [Test]
    public void SettingsViewModel_LogsMissingDistrictWiring()
    {
        var vm = XamlViewFile.Read("ViewModels/Settings/SettingsViewModel.cs");
        Assert.That(vm, Does.Contain("IDistrictSettingsAccessor"));
        Assert.That(vm, Does.Contain("IDistrictMapSync"));
        Assert.That(vm, Does.Contain("depot/bbox will not overlay"));
        Assert.That(vm, Does.Contain("save will not recenter"));
    }
}
