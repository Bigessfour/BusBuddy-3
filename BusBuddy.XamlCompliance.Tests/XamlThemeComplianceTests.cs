using FluentAssertions;
using NUnit.Framework;

namespace BusBuddy.XamlCompliance.Tests;

/// <summary>
/// Automated theme-alignment gates for all BusBuddy WPF Views/Controls XAML.
/// Targets net9.0 (no WindowsDesktop) so Mac hybrid hosts and CI both run these gates.
/// </summary>
[TestFixture]
[Category("Unit")]
[Category("Theme")]
[Category("UI")]
public sealed class XamlThemeComplianceTests
{
    private string _repoRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _repoRoot = XamlThemeComplianceScanner.FindRepoRoot();
    }

    [Test]
    public void UiXaml_Should_Have_No_Theme_Misalignment_Findings()
    {
        var files = XamlThemeComplianceScanner.EnumerateUiXamlFiles(_repoRoot);
        files.Should().NotBeEmpty("expected Views/Controls XAML under BusBuddy.WPF");

        var findings = XamlThemeComplianceScanner.ScanUiXaml(_repoRoot);
        findings.Should().BeEmpty(
            "theme misalignment findings:\n{0}",
            XamlThemeComplianceScanner.FormatFindings(findings));
    }

    [Test]
    public void FluentDark_And_FluentLight_Should_Define_Matching_BusBuddy_Brush_Keys()
    {
        var findings = XamlThemeComplianceScanner.ScanThemeDictionaryParity(_repoRoot)
            .Where(f => f.Rule is "ThemeBrushKeyParity" or "MissingRequiredThemeBrush")
            .ToList();

        findings.Should().BeEmpty(
            "theme dictionary parity findings:\n{0}",
            XamlThemeComplianceScanner.FormatFindings(findings));
    }

    [Test]
    public void Scanner_Should_Detect_Known_Violation_Patterns()
    {
        const string sample = """
            <UserControl xmlns:syncfusion="http://schemas.syncfusion.com/wpf">
              <syncfusion:ButtonAdv Label="🔄 Refresh" Content="Ignored" Background="#F5F5F5" Foreground="White"/>
              <syncfusion:ButtonAdv Label="Save"
                Background="{DynamicResource BusBuddy.Brush.Primary}"
                Foreground="{DynamicResource BusBuddy.Brush.Text.Primary}"/>
              <Border Background="{DynamicResource BusBuddy.Brush.Primary}">
                <TextBlock Foreground="{DynamicResource BusBuddy.Brush.Text.Primary}" Text="Title"/>
              </Border>
              <TextBox Watermark="\ud83d\udd0d Quick search"/>
              <Border Background="{StaticResource BusBuddy.Brush.Panel.Header}"/>
            </UserControl>
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), "busbuddy-theme-scan-" + Guid.NewGuid().ToString("N"));
        var views = Path.Combine(tempDir, "BusBuddy.WPF", "Views");
        Directory.CreateDirectory(views);
        File.WriteAllText(Path.Combine(tempDir, "BusBuddy.sln"), "Microsoft Visual Studio Solution File");
        File.WriteAllText(Path.Combine(views, "BadView.xaml"), sample);

        try
        {
            var findings = XamlThemeComplianceScanner.ScanUiXaml(tempDir);
            findings.Select(f => f.Rule).Should().Contain([
                "EmojiInButtonAdvLabel",
                "ButtonAdvContentIgnored",
                "HardcodedLightChrome",
                "HardcodedWhiteForeground",
                "TextPrimaryOnColoredBackground",
                "TextPrimaryOnPrimaryHeader",
                "EmojiInWatermark",
                "StaticBusBuddyBrush",
            ]);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
