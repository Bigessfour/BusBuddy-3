using BusBuddy.WPF.Utilities;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF
{
    [TestFixture]
    [Category("WPF")]
    public class SyncfusionThemeManagerTests
    {
        [TestCase(null, "FluentDark")]
        [TestCase("", "FluentDark")]
        [TestCase("FluentDark", "FluentDark")]
        [TestCase("fluentdark", "FluentDark")]
        [TestCase("FluentLight", "FluentLight")]
        [TestCase("light", "FluentLight")]
        [TestCase("FluentWhite", "FluentLight")]
        public void NormalizeThemeName_MapsAliases(string? input, string expected)
        {
            Assert.That(SyncfusionThemeManager.NormalizeThemeName(input), Is.EqualTo(expected));
        }

        [Test]
        public void ThemeDictionaryPath_MatchesActiveTheme()
        {
            Assert.That(SyncfusionThemeManager.ThemeDictionaryPath("FluentDark"), Does.Contain("FluentDarkTheme.xaml"));
            Assert.That(SyncfusionThemeManager.ThemeDictionaryPath("FluentLight"), Does.Contain("FluentLightTheme.xaml"));
        }
    }
}
