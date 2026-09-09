using System;
using System.Threading;
using System.Threading.Tasks;
using BusBuddy.WPF.ViewModels.Map;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BusBuddy.WPF.Services;

/// <summary>Resolves the singleton <see cref="MapViewModel"/> and applies district geography.</summary>
public sealed class DistrictMapSync : IDistrictMapSync
{
    private static readonly ILogger Logger = Log.ForContext<DistrictMapSync>();
    private readonly IServiceProvider _services;

    public DistrictMapSync(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public async Task ApplyDistrictGeographyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var map = _services.GetService<MapViewModel>();
        if (map is null)
        {
            Logger.Debug("District map sync skipped — MapViewModel not registered");
            return;
        }

        await map.ApplyDistrictSettingsAsync().ConfigureAwait(false);
    }
}
