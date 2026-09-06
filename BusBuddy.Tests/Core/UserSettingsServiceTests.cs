using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace BusBuddy.Tests.Core;

[TestFixture]
[Category("Unit")]
public class UserSettingsServiceTests
{
    [Test]
    public async Task LoadSave_PersistsThemeAndBooleanFlags()
    {
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-settings-{Guid.NewGuid():N}.json");
        try
        {
            var service = new UserSettingsService(path);

            await service.SetSettingAsync(UserSettingsKeys.Theme, "FluentLight");
            await service.SetSettingAsync(UserSettingsKeys.EnableActivityLogging, false);
            await service.SetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, false);
            (await service.SaveSettingsAsync()).Should().BeTrue();

            var reloaded = new UserSettingsService(path);
            await reloaded.LoadSettingsAsync();

            (await reloaded.GetSettingAsync(UserSettingsKeys.Theme, "FluentDark")).Should().Be("FluentLight");
            (await reloaded.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, true)).Should().BeFalse();
            (await reloaded.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, true)).Should().BeFalse();
            reloaded.EnableActivityLogging.Should().BeFalse();
            reloaded.ShowDashboardOnStartup.Should().BeFalse();
            reloaded.CachedTheme.Should().Be("FluentLight");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task ResetSettingsAsync_ClearsFileAndRestoresDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-settings-{Guid.NewGuid():N}.json");
        try
        {
            var service = new UserSettingsService(path);
            await service.SetSettingAsync(UserSettingsKeys.Theme, "FluentLight");
            await service.SaveSettingsAsync();
            File.Exists(path).Should().BeTrue();

            (await service.ResetSettingsAsync()).Should().BeTrue();
            File.Exists(path).Should().BeFalse();
            service.CachedTheme.Should().Be("FluentDark");
            service.EnableActivityLogging.Should().BeTrue();
            service.ShowDashboardOnStartup.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

[TestFixture]
[Category("Unit")]
public class ActivityLogServiceSettingsTests
{
    [Test]
    public async Task LogAsync_WhenActivityLoggingDisabled_DoesNotWriteRow()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"activity-settings-{Guid.NewGuid():N}")
            .Options;

        await using var db = new BusBuddyDbContext(options);
        var path = Path.Combine(Path.GetTempPath(), $"busbuddy-settings-{Guid.NewGuid():N}.json");
        try
        {
            var settings = new UserSettingsService(path);
            await settings.SetSettingAsync(UserSettingsKeys.EnableActivityLogging, false);
            await settings.SaveSettingsAsync();
            await settings.LoadSettingsAsync();

            var service = new ActivityLogService(db, settings);
            await service.LogAsync("TestAction", "tester", "details");

            db.ActivityLogs.Count().Should().Be(0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public async Task LogAsync_WhenActivityLoggingEnabled_WritesRow()
    {
        var options = new DbContextOptionsBuilder<BusBuddyDbContext>()
            .UseInMemoryDatabase($"activity-settings-{Guid.NewGuid():N}")
            .Options;

        await using var db = new BusBuddyDbContext(options);
        var settings = new UserSettingsService(Path.Combine(Path.GetTempPath(), $"busbuddy-settings-{Guid.NewGuid():N}.json"));
        await settings.LoadSettingsAsync();

        var service = new ActivityLogService(db, settings);
        await service.LogAsync("TestAction", "tester", "details");

        db.ActivityLogs.Count().Should().Be(1);
    }
}
