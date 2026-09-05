using System.Threading.Tasks;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.ViewModels.Settings;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.WPF;

[TestFixture]
[Category("Unit")]
[Category("UI")]
public class SettingsViewModelTests
{
    [Test]
    public async Task SaveSettingsAsync_PersistsAllKeysAndUpdatesStatus()
    {
        var settings = new Mock<IUserSettingsService>();
        settings.SetupGet(s => s.EnableActivityLogging).Returns(true);
        settings.SetupGet(s => s.ShowDashboardOnStartup).Returns(true);
        settings.SetupGet(s => s.CachedTheme).Returns("FluentDark");
        settings.Setup(s => s.LoadSettingsAsync()).Returns(Task.CompletedTask);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.Theme, It.IsAny<string>()))
            .ReturnsAsync("FluentDark");
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.SaveSettingsAsync()).ReturnsAsync(true);

        var skin = new Mock<ISkinManagerService>();
        var vm = new SettingsViewModel(settings.Object, skin.Object);

        await Task.Delay(100);
        vm.SelectedTheme = "FluentLight";
        vm.EnableActivityLogging = false;
        vm.ShowDashboardOnStartup = false;

        await vm.SaveCommand.ExecuteAsync(null);

        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.Theme, "FluentLight"), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.EnableActivityLogging, false), Times.Once);
        settings.Verify(s => s.SetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, false), Times.Once);
        settings.Verify(s => s.SaveSettingsAsync(), Times.Once);
        skin.Verify(s => s.ApplyTheme("FluentLight"), Times.Once);
        vm.StatusMessage.Should().Be("Settings saved");
    }

    [Test]
    public async Task ResetSettingsAsync_ReloadsDefaults()
    {
        var settings = new Mock<IUserSettingsService>();
        settings.SetupGet(s => s.EnableActivityLogging).Returns(true);
        settings.SetupGet(s => s.ShowDashboardOnStartup).Returns(true);
        settings.SetupGet(s => s.CachedTheme).Returns("FluentDark");
        settings.Setup(s => s.LoadSettingsAsync()).Returns(Task.CompletedTask);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.Theme, It.IsAny<string>()))
            .ReturnsAsync("FluentDark");
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, It.IsAny<bool>()))
            .ReturnsAsync(true);
        settings.Setup(s => s.ResetSettingsAsync()).ReturnsAsync(true);

        var vm = new SettingsViewModel(settings.Object, new Mock<ISkinManagerService>().Object);
        await Task.Delay(100);

        await vm.ResetCommand.ExecuteAsync(null);

        settings.Verify(s => s.ResetSettingsAsync(), Times.Once);
        settings.Verify(s => s.LoadSettingsAsync(), Times.AtLeastOnce);
        vm.StatusMessage.Should().Be("Settings reset to defaults");
    }
}
