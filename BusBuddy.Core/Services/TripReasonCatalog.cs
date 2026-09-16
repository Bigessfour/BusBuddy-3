using Serilog;

namespace BusBuddy.Core.Services;

/// <inheritdoc cref="ITripReasonCatalog"/>
public sealed class TripReasonCatalog : ITripReasonCatalog
{
    private static readonly ILogger Logger = Log.ForContext<TripReasonCatalog>();

    public static readonly string[] SeedPurposes = ["Sports", "Field Trip"];

    public static readonly string[] SeedSports =
    [
        "Football",
        "Basketball",
        "Baseball",
        "Volleyball",
        "JH Football",
        "JH Basketball",
        "JH Baseball",
        "JH Volleyball",
        "Wrestling",
        "JH Wrestling",
        "Softball"
    ];

    private readonly IUserSettingsService _userSettings;

    public TripReasonCatalog(IUserSettingsService userSettings)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
    }

    public Task<IReadOnlyList<string>> GetPurposesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync(UserSettingsKeys.TripPurposes, SeedPurposes, cancellationToken);

    public Task<IReadOnlyList<string>> GetSportsAsync(CancellationToken cancellationToken = default) =>
        GetListAsync(UserSettingsKeys.TripSports, SeedSports, cancellationToken);

    public Task RememberPurposeAsync(string purpose, CancellationToken cancellationToken = default) =>
        RememberAsync(UserSettingsKeys.TripPurposes, SeedPurposes, purpose, cancellationToken);

    public Task RememberSportAsync(string sport, CancellationToken cancellationToken = default) =>
        RememberAsync(UserSettingsKeys.TripSports, SeedSports, sport, cancellationToken);

    public Task ForgetPurposeAsync(string purpose, CancellationToken cancellationToken = default) =>
        ForgetAsync(UserSettingsKeys.TripPurposes, SeedPurposes, purpose, cancellationToken);

    public Task ForgetSportAsync(string sport, CancellationToken cancellationToken = default) =>
        ForgetAsync(UserSettingsKeys.TripSports, SeedSports, sport, cancellationToken);

    private async Task<IReadOnlyList<string>> GetListAsync(
        string key,
        IReadOnlyList<string> seeds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var saved = await _userSettings.GetSettingAsync(key, new List<string>()).ConfigureAwait(false)
                    ?? [];
        if (saved.Count == 0)
        {
            return seeds.ToList();
        }

        return Normalize(saved);
    }

    private async Task RememberAsync(
        string key,
        IReadOnlyList<string> seeds,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var saved = await LoadOrSeedAsync(key, seeds).ConfigureAwait(false);
        if (saved.Any(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        saved.Add(trimmed);
        await PersistAsync(key, saved).ConfigureAwait(false);
        Logger.Information("Remembered trip catalog {Key} entry {Value}", key, trimmed);
    }

    private async Task ForgetAsync(
        string key,
        IReadOnlyList<string> seeds,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var saved = await LoadOrSeedAsync(key, seeds).ConfigureAwait(false);
        var remaining = saved
            .Where(s => !string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await PersistAsync(key, remaining).ConfigureAwait(false);
        Logger.Information("Removed trip catalog {Key} entry {Value}", key, trimmed);
    }

    private async Task<List<string>> LoadOrSeedAsync(string key, IReadOnlyList<string> seeds)
    {
        var saved = await _userSettings.GetSettingAsync(key, new List<string>()).ConfigureAwait(false)
                    ?? [];
        var normalized = Normalize(saved);
        return saved.Count == 0 ? seeds.ToList() : normalized;
    }

    private async Task PersistAsync(string key, List<string> values)
    {
        await _userSettings.SetSettingAsync(key, Normalize(values)).ConfigureAwait(false);
        await _userSettings.SaveSettingsAsync().ConfigureAwait(false);
    }

    private static List<string> Normalize(IEnumerable<string?> values) =>
        values
            .Select(v => v?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
}
