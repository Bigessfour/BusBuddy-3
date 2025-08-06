using System.ComponentModel.DataAnnotations;

namespace BusBuddy.Core.Configuration;

/// <summary>
/// Application-specific configuration options.
/// Maps to the AppSettings section in appsettings.azure.json.
/// </summary>
public class AppSettingsOptions
{
    public const string SectionName = "AppSettings";

    public string Theme { get; set; } = "Office2019Colorful";
    public bool AutoSave { get; set; } = true;

    [Range(60, 3600)]
    public int AutoSaveInterval { get; set; } = 300;

    [Range(1, 50)]
    public int MaxRecentFiles { get; set; } = 10;

    public string DatabaseProvider { get; set; } = "Local";
    public bool EnableDetailedLogging { get; set; } = true;
    public bool AutoMigrateDatabase { get; set; } = true;

    [Range(10, 500)]
    public int DefaultPageSize { get; set; } = 25;

    [Range(50, 10000)]
    public int MaxSearchResults { get; set; } = 100;
}
