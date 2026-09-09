using System.Threading;
using System.Threading.Tasks;

namespace BusBuddy.WPF.Services;

/// <summary>
/// After clerk Settings persist depot/bbox, refresh the singleton District Map
/// (depot pin + camera). Keeps SettingsViewModel free of a hard MapViewModel dependency.
/// </summary>
public interface IDistrictMapSync
{
    Task ApplyDistrictGeographyAsync(CancellationToken cancellationToken = default);
}
