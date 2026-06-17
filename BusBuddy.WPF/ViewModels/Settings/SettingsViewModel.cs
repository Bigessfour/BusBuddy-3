using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusBuddy.WPF.ViewModels.Settings
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IUserSettingsService _settingsService;
        private readonly ISkinManagerService _skinManagerService;

        public SettingsViewModel(IUserSettingsService settingsService, ISkinManagerService skinManagerService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _skinManagerService = skinManagerService ?? throw new ArgumentNullException(nameof(skinManagerService));

            AvailableThemes = new ObservableCollection<string> { "FluentDark", "FluentLight" };
            SaveCommand = new AsyncRelayCommand(SaveSettingsAsync);
            ResetCommand = new AsyncRelayCommand(ResetSettingsAsync);

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
        private string statusMessage = "Loading settings...";

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        partial void OnSelectedThemeChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _skinManagerService.ApplyTheme(value);
            }
        }

        private async Task LoadSettingsAsync()
        {
            await _settingsService.LoadSettingsAsync();
            SelectedTheme = await _settingsService.GetSettingAsync("Theme", "FluentDark");
            EnableActivityLogging = await _settingsService.GetSettingAsync("EnableActivityLogging", true);
            ShowDashboardOnStartup = await _settingsService.GetSettingAsync("ShowDashboardOnStartup", true);
            StatusMessage = "Settings loaded";
        }

        private async Task SaveSettingsAsync()
        {
            await _settingsService.SetSettingAsync("Theme", SelectedTheme);
            await _settingsService.SetSettingAsync("EnableActivityLogging", EnableActivityLogging);
            await _settingsService.SetSettingAsync("ShowDashboardOnStartup", ShowDashboardOnStartup);
            var saved = await _settingsService.SaveSettingsAsync();
            StatusMessage = saved ? "Settings saved" : "Failed to save settings";
        }

        private async Task ResetSettingsAsync()
        {
            await _settingsService.ResetSettingsAsync();
            await LoadSettingsAsync();
            StatusMessage = "Settings reset to defaults";
        }
    }
}
