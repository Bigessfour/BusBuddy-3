using System;
using System.IO;
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
        [TestCase("Office2019Colorful", "FluentDark")]
        [TestCase("Office2019", "FluentDark")]
        public void NormalizeThemeName_MapsAliases(string? input, string expected)
        {
            Assert.That(SyncfusionThemeManager.NormalizeThemeName(input), Is.EqualTo(expected));
        }

        [Test]
        public void ThemeDictionaryPath_MatchesActiveTheme()
        {
            Assert.That(SyncfusionThemeManager.ThemeDictionaryPath("FluentDark"), Does.Contain("FluentDarkTheme.xaml"));
            Assert.That(SyncfusionThemeManager.ThemeDictionaryPath("FluentLight"), Does.Contain("FluentLightTheme.xaml"));
            Assert.That(SyncfusionThemeManager.ThemeDictionaryPath("Office2019Colorful"), Does.Contain("FluentDarkTheme.xaml"));
        }
    }

    [TestFixture]
    [Category("WPF")]
    public class ThemePreferenceStoreTests
    {
        private string _originalPath = null!;
        private string _tempDir = null!;

        [SetUp]
        public void SetUp()
        {
            _originalPath = ThemePreferenceStore.SettingsFilePath;
            _tempDir = Path.Combine(Path.GetTempPath(), "busbuddy-theme-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            ThemePreferenceStore.SettingsFilePath = Path.Combine(_tempDir, "user-settings.json");
        }

        [TearDown]
        public void TearDown()
        {
            ThemePreferenceStore.SettingsFilePath = _originalPath;
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Test]
        public void Load_ReturnsFallback_WhenFileMissing()
        {
            Assert.That(ThemePreferenceStore.Load(), Is.EqualTo("FluentDark"));
        }

        [Test]
        public void Save_ThenLoad_RoundTripsFluentLight()
        {
            ThemePreferenceStore.Save("FluentLight");
            Assert.That(ThemePreferenceStore.Load(), Is.EqualTo("FluentLight"));
            Assert.That(File.ReadAllText(ThemePreferenceStore.SettingsFilePath), Does.Contain("FluentLight"));
        }

        [Test]
        public void Save_MergesWithoutDroppingOtherKeys()
        {
            File.WriteAllText(ThemePreferenceStore.SettingsFilePath, """{"EnableActivityLogging":true,"Theme":"FluentDark"}""");
            ThemePreferenceStore.Save("FluentLight");
            var json = File.ReadAllText(ThemePreferenceStore.SettingsFilePath);
            Assert.That(json, Does.Contain("FluentLight"));
            Assert.That(json, Does.Contain("EnableActivityLogging").Or.Contain("enableActivityLogging"));
        }

        [Test]
        public void Load_MapsRetiredOfficeThemeToFluentDark()
        {
            File.WriteAllText(ThemePreferenceStore.SettingsFilePath, """{"Theme":"Office2019Colorful"}""");
            Assert.That(ThemePreferenceStore.Load(), Is.EqualTo("FluentDark"));
        }
    }
}
