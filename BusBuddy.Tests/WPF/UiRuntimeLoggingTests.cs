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
    }

    [Test]
    public void AppStartup_WiresUiDiagnosticsAndCapabilitySnapshot()
    {
        var source = XamlViewFile.Read("App.xaml.cs");
        Assert.That(source, Does.Contain("WpfTraceSerilogListener.Attach"));
        Assert.That(source, Does.Contain("UiSurfaceProbe.Register"));
        Assert.That(source, Does.Contain("RuntimeCapabilityLogger.WriteStartupSnapshot"));
        Assert.That(source, Does.Contain("LoggingModeManager.Initialize"));
        Assert.That(source, Does.Contain("OnUnobservedTaskException"));
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
