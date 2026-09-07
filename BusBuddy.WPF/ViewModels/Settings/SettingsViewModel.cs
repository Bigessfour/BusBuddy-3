using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BusBuddy.Core.Configuration;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Services;
using BusBuddy.WPF.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;

namespace BusBuddy.WPF.ViewModels.Settings
{
    public partial class SettingsViewModel : ObservableObject
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsViewModel>();

        private readonly IUserSettingsService _settingsService;
        private readonly ISkinManagerService _skinManagerService;
        private readonly IDistrictSettingsAccessor? _districtSettings;
        private readonly RoutingDistrictSettings _appDistrict;
        private bool _suppressThemePreview;

        public SettingsViewModel(
            IUserSettingsService settingsService,
            ISkinManagerService skinManagerService,
            IDistrictSettingsAccessor? districtSettings = null,
            IOptions<RoutingDistrictSettings>? routingDistrict = null)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _skinManagerService = skinManagerService ?? throw new ArgumentNullException(nameof(skinManagerService));
            _districtSettings = districtSettings;
            _appDistrict = routingDistrict?.Value ?? new RoutingDistrictSettings();

            AvailableThemes = new ObservableCollection<string> { "FluentDark", "FluentLight" };
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync, CanSave);
            ResetCommand = new AsyncRelayCommand(ResetSettingsAsync, () => !IsBusy);

            _ = LoadSettingsAsync();
        }

        public ObservableCollection<string> AvailableThemes { get; }

        [ObservableProperty]
        private string selectedTheme = "FluentDark";

        [ObservableProperty]
        private bool enableActivityLogging = true;

        [ObservableProperty]
        private bool showDashboardOnStartup = true;

        [ObservableProperty]
        private bool enableRouteGeoExport;

        [ObservableProperty]
        private string depotName = string.Empty;

        [ObservableProperty]
        private string depotAddress = string.Empty;

        [ObservableProperty]
        private string depotCity = string.Empty;

        [ObservableProperty]
        private string depotState = string.Empty;

        [ObservableProperty]
        private string depotZipCode = string.Empty;

        [ObservableProperty]
        private string depotLatitudeText = string.Empty;

        [ObservableProperty]
        private string depotLongitudeText = string.Empty;

        [ObservableProperty]
        private string boundingBoxMinLatText = string.Empty;

        [ObservableProperty]
        private string boundingBoxMinLonText = string.Empty;

        [ObservableProperty]
        private string boundingBoxMaxLatText = string.Empty;

        [ObservableProperty]
        private string boundingBoxMaxLonText = string.Empty;

        [ObservableProperty]
        private string statusMessage = "Loading settings...";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
        private bool isBusy;

        public IAsyncRelayCommand SaveCommand { get; }
        public IAsyncRelayCommand ResetCommand { get; }

        partial void OnSelectedThemeChanged(string value)
        {
            if (_suppressThemePreview || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            Logger.Information("Previewing theme {Theme}", value);
            SyncfusionThemeManager.ApplyApplicationThemePreview(value);
        }

        private bool CanSave() => !IsBusy;

        private async Task LoadSettingsAsync()
        {
            using (LogContext.PushProperty("Operation", "LoadSettings"))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Loading settings...";
                    Logger.Information("Loading user settings");

                    _suppressThemePreview = true;
                    await _settingsService.LoadSettingsAsync().ConfigureAwait(true);
                    SelectedTheme = await _settingsService.GetSettingAsync(UserSettingsKeys.Theme, "FluentDark").ConfigureAwait(true);
                    EnableActivityLogging = await _settingsService.GetSettingAsync(UserSettingsKeys.EnableActivityLogging, true).ConfigureAwait(true);
                    ShowDashboardOnStartup = await _settingsService.GetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, true).ConfigureAwait(true);
                    EnableRouteGeoExport = await _settingsService.GetSettingAsync(UserSettingsKeys.EnableRouteGeoExport, false).ConfigureAwait(true);
                    _districtSettings?.OverlayFromUserSettings(_settingsService);
                    HydrateDistrictFields(_districtSettings?.Current ?? _appDistrict);
                    _suppressThemePreview = false;

                    SyncfusionThemeManager.ApplyApplicationThemePreview(SelectedTheme);
                    StatusMessage = "Settings loaded";
                    Logger.Information(
                        "Settings loaded Theme={Theme} ActivityLogging={ActivityLogging} DashboardOnStartup={DashboardOnStartup}",
                        SelectedTheme,
                        EnableActivityLogging,
                        ShowDashboardOnStartup);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error loading settings");
                    StatusMessage = "Error loading settings";
                }
                finally
                {
                    _suppressThemePreview = false;
                    IsBusy = false;
                }
            }
        }

        private async Task SaveSettingsAsync()
        {
            using (LogContext.PushProperty("Operation", "SaveSettings"))
            {
                try
                {
                    IsBusy = true;
                    StatusMessage = "Saving settings...";
                    Logger.Information(
                        "Saving settings Theme={Theme} ActivityLogging={ActivityLogging} DashboardOnStartup={DashboardOnStartup}",
                        SelectedTheme,
                        EnableActivityLogging,
                        ShowDashboardOnStartup);

                    await _settingsService.SetSettingAsync(UserSettingsKeys.Theme, SelectedTheme).ConfigureAwait(true);
                    await _settingsService.SetSettingAsync(UserSettingsKeys.EnableActivityLogging, EnableActivityLogging).ConfigureAwait(true);
                    await _settingsService.SetSettingAsync(UserSettingsKeys.ShowDashboardOnStartup, ShowDashboardOnStartup).ConfigureAwait(true);
                    await _settingsService.SetSettingAsync(UserSettingsKeys.EnableRouteGeoExport, EnableRouteGeoExport).ConfigureAwait(true);
                    var district = DistrictFromForm();
                    await DistrictSettingsAccessor.WriteToUserAsync(_settingsService, district).ConfigureAwait(true);
                    _districtSettings?.Replace(district);

                    _skinManagerService.ApplyTheme(SelectedTheme);
                    var saved = await _settingsService.SaveSettingsAsync().ConfigureAwait(true);
                    StatusMessage = saved ? "Settings saved" : "Failed to save settings";

                    if (saved)
                    {
                        Logger.Information("Settings saved successfully");
                    }
                    else
                    {
                        Logger.Warning("Settings save returned false");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error saving settings");
                    StatusMessage = "Error saving settings";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async Task ResetSettingsAsync()
        {
            using (LogContext.PushProperty("Operation", "ResetSettings"))
            {
                try
                {
                    IsBusy = true;
                    Logger.Information("Resetting settings to defaults");
                    await _settingsService.ResetSettingsAsync().ConfigureAwait(true);
                    _districtSettings?.Replace(_appDistrict);
                    await LoadSettingsAsync().ConfigureAwait(true);
                    StatusMessage = "Settings reset to defaults";
                    Logger.Information("Settings reset completed");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error resetting settings");
                    StatusMessage = "Error resetting settings";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private void HydrateDistrictFields(RoutingDistrictSettings district)
        {
            DepotName = district.DepotName ?? string.Empty;
            DepotAddress = district.DepotAddress ?? string.Empty;
            DepotCity = district.DepotCity ?? string.Empty;
            DepotState = district.DepotState ?? string.Empty;
            DepotZipCode = district.DepotZipCode ?? string.Empty;
            DepotLatitudeText = DistrictSettingsAccessor.FormatCoord(district.DepotLatitude);
            DepotLongitudeText = DistrictSettingsAccessor.FormatCoord(district.DepotLongitude);
            BoundingBoxMinLatText = DistrictSettingsAccessor.FormatCoord(district.BoundingBoxMinLat);
            BoundingBoxMinLonText = DistrictSettingsAccessor.FormatCoord(district.BoundingBoxMinLon);
            BoundingBoxMaxLatText = DistrictSettingsAccessor.FormatCoord(district.BoundingBoxMaxLat);
            BoundingBoxMaxLonText = DistrictSettingsAccessor.FormatCoord(district.BoundingBoxMaxLon);
        }

        private RoutingDistrictSettings DistrictFromForm()
        {
            var next = DistrictSettingsAccessor.Copy(_appDistrict);
            next.DepotName = NullIfBlank(DepotName);
            next.DepotAddress = NullIfBlank(DepotAddress);
            next.DepotCity = NullIfBlank(DepotCity);
            next.DepotState = NullIfBlank(DepotState);
            next.DepotZipCode = NullIfBlank(DepotZipCode);
            next.DepotLatitude = DistrictSettingsAccessor.ParseCoord(DepotLatitudeText);
            next.DepotLongitude = DistrictSettingsAccessor.ParseCoord(DepotLongitudeText);
            next.BoundingBoxMinLat = DistrictSettingsAccessor.ParseCoord(BoundingBoxMinLatText);
            next.BoundingBoxMinLon = DistrictSettingsAccessor.ParseCoord(BoundingBoxMinLonText);
            next.BoundingBoxMaxLat = DistrictSettingsAccessor.ParseCoord(BoundingBoxMaxLatText);
            next.BoundingBoxMaxLon = DistrictSettingsAccessor.ParseCoord(BoundingBoxMaxLonText);
            return next;
        }

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
