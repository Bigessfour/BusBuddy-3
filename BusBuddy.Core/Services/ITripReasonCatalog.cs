namespace BusBuddy.Core.Services;

/// <summary>
/// Clerk-maintained trip purposes and sports/reasons. Lives in user-settings.json, not code.
/// </summary>
public interface ITripReasonCatalog
{
    Task<IReadOnlyList<string>> GetPurposesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSportsAsync(CancellationToken cancellationToken = default);

    Task RememberPurposeAsync(string purpose, CancellationToken cancellationToken = default);

    Task RememberSportAsync(string sport, CancellationToken cancellationToken = default);

    Task ForgetPurposeAsync(string purpose, CancellationToken cancellationToken = default);

    Task ForgetSportAsync(string sport, CancellationToken cancellationToken = default);
}
