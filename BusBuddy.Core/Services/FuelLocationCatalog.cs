using Serilog;

namespace BusBuddy.Core.Services;

/// <inheritdoc cref="IFuelLocationCatalog"/>
public sealed class FuelLocationCatalog : IFuelLocationCatalog
{
    private static readonly ILogger Logger = Log.ForContext<FuelLocationCatalog>();

    /// <summary>Starter options when catalog and DB are empty.</summary>
    public static readonly string[] SeedDefaults =
    [
        "Key Pumps",
        "School District Pump"
    ];

    private readonly IUserSettingsService _userSettings;
    private readonly IFuelService _fuelService;

    public FuelLocationCatalog(IUserSettingsService userSettings, IFuelService fuelService)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        _fuelService = fuelService ?? throw new ArgumentNullException(nameof(fuelService));
    }

    public async Task<IReadOnlyList<string>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var saved = await _userSettings.GetSettingAsync(UserSettingsKeys.FuelLocations, new List<string>())
            ?? new List<string>();
        var fromDb = await _fuelService.GetDistinctFuelLocationsAsync();

        var merged = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in SeedDefaults)
        {
            merged.Add(seed);
        }

        foreach (var name in saved)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                merged.Add(name.Trim());
            }
        }

        foreach (var name in fromDb)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                merged.Add(name.Trim());
            }
        }

        return merged.ToList();
    }

    public async Task RememberAsync(string location, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var trimmed = location?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var saved = await _userSettings.GetSettingAsync(UserSettingsKeys.FuelLocations, new List<string>())
            ?? new List<string>();

        if (saved.Any(s => string.Equals(s?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        saved.Add(trimmed);
        await _userSettings.SetSettingAsync(UserSettingsKeys.FuelLocations, saved);
        await _userSettings.SaveSettingsAsync();
        Logger.Information("Remembered fuel location catalog entry {FuelLocation}", trimmed);
    }
}
