using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using BusBuddy.Core.Services;
using BusBuddy.WPF.ViewModels;
using Microsoft.Extensions.Configuration;

namespace BusBuddy.WPF.ViewModels.Settings
{
    /// <summary>
    /// ViewModel for the Settings view providing user configuration options
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IUserSettingsService _userSettingsService;
        private readonly IConfiguration _configuration;
        private new static readonly ILogger Logger = Log.ForContext<SettingsViewModel>();

        #region Constructor
        public SettingsViewModel(IUserSettingsService userSettingsService, IConfiguration configuration)
        {
            _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // Initialize commands
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync);
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);

            // Load settings on initialization
            LoadSettingsAsync().ConfigureAwait(false);
        }
        #endregion

        #region Properties

        #region General Settings
        private bool _enableLogging = true;
        public bool EnableLogging
        {
            get => _enableLogging;
            set => SetProperty(ref _enableLogging, value);
        }

        private bool _enableAutoSave = true;
        public bool EnableAutoSave
        {
            get => _enableAutoSave;
            set => SetProperty(ref _enableAutoSave, value);
        }

        private int _maxRecordsPerPage = 50;
        public int MaxRecordsPerPage
        {
            get => _maxRecordsPerPage;
            set => SetProperty(ref _maxRecordsPerPage, value);
        }
        #endregion

        #region UI Settings
        private string _theme = "FluentDark";
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        private bool _enableAnimations = true;
        public bool EnableAnimations
        {
            get => _enableAnimations;
            set => SetProperty(ref _enableAnimations, value);
        }

        private int _fontSize = 12;
        public int FontSize
        {
            get => _fontSize;
            set => SetProperty(ref _fontSize, value);
        }
        #endregion

        #region Route Planning Settings
        private bool _enableRouteOptimization = true;
        public bool EnableRouteOptimization
        {
            get => _enableRouteOptimization;
            set => SetProperty(ref _enableRouteOptimization, value);
        }

        private int _defaultStopDuration = 2;
        public int DefaultStopDuration
        {
            get => _defaultStopDuration;
            set => SetProperty(ref _defaultStopDuration, value);
        }

        private bool _autoAssignStudents;
        public bool AutoAssignStudents
        {
            get => _autoAssignStudents;
            set => SetProperty(ref _autoAssignStudents, value);
        }
        #endregion

        #region Data Management Settings
        private bool _enableDataBackup = true;
        public bool EnableDataBackup
        {
            get => _enableDataBackup;
            set => SetProperty(ref _enableDataBackup, value);
        }

        private int _backupFrequencyDays = 7;
        public int BackupFrequencyDays
        {
            get => _backupFrequencyDays;
            set => SetProperty(ref _backupFrequencyDays, value);
        }

        private bool _enableDataValidation = true;
        public bool EnableDataValidation
        {
            get => _enableDataValidation;
            set => SetProperty(ref _enableDataValidation, value);
        }
        #endregion

        #region Notification Settings
        private bool _enableSystemNotifications = true;
        public bool EnableSystemNotifications
        {
            get => _enableSystemNotifications;
            set => SetProperty(ref _enableSystemNotifications, value);
        }

        private bool _enableEmailNotifications;
        public bool EnableEmailNotifications
        {
            get => _enableEmailNotifications;
            set => SetProperty(ref _enableEmailNotifications, value);
        }

        private bool _notifyOnRouteCompletion = true;
        public bool NotifyOnRouteCompletion
        {
            get => _notifyOnRouteCompletion;
            set => SetProperty(ref _notifyOnRouteCompletion, value);
        }
        #endregion

        #endregion

        #region Commands
        public IAsyncRelayCommand SaveSettingsCommand { get; }
        public IAsyncRelayCommand ResetSettingsCommand { get; }
        public IAsyncRelayCommand LoadSettingsCommand { get; }
        #endregion

        #region Methods

        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading settings...";

                Logger.Information("Loading user settings");

                // Load general settings
                EnableLogging = await _userSettingsService.GetSettingAsync("EnableLogging", true);
                EnableAutoSave = await _userSettingsService.GetSettingAsync("EnableAutoSave", true);
                MaxRecordsPerPage = await _userSettingsService.GetSettingAsync("MaxRecordsPerPage", 50);

                // Load UI settings
                Theme = await _userSettingsService.GetSettingAsync("Theme", "FluentDark");
                EnableAnimations = await _userSettingsService.GetSettingAsync("EnableAnimations", true);
                FontSize = await _userSettingsService.GetSettingAsync("FontSize", 12);

                // Load route planning settings
                EnableRouteOptimization = await _userSettingsService.GetSettingAsync("EnableRouteOptimization", true);
                DefaultStopDuration = await _userSettingsService.GetSettingAsync("DefaultStopDuration", 2);
                AutoAssignStudents = await _userSettingsService.GetSettingAsync("AutoAssignStudents", false);

                // Load data management settings
                EnableDataBackup = await _userSettingsService.GetSettingAsync("EnableDataBackup", true);
                BackupFrequencyDays = await _userSettingsService.GetSettingAsync("BackupFrequencyDays", 7);
                EnableDataValidation = await _userSettingsService.GetSettingAsync("EnableDataValidation", true);

                // Load notification settings
                EnableSystemNotifications = await _userSettingsService.GetSettingAsync("EnableSystemNotifications", true);
                EnableEmailNotifications = await _userSettingsService.GetSettingAsync("EnableEmailNotifications", false);
                NotifyOnRouteCompletion = await _userSettingsService.GetSettingAsync("NotifyOnRouteCompletion", true);

                StatusMessage = "Settings loaded successfully";
                Logger.Information("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load settings");
                StatusMessage = $"Failed to load settings: {ex.Message}";
                MessageBox.Show($"Failed to load settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Saving settings...";

                Logger.Information("Saving user settings");

                // Save general settings
                await _userSettingsService.SetSettingAsync("EnableLogging", EnableLogging);
                await _userSettingsService.SetSettingAsync("EnableAutoSave", EnableAutoSave);
                await _userSettingsService.SetSettingAsync("MaxRecordsPerPage", MaxRecordsPerPage);

                // Save UI settings
                await _userSettingsService.SetSettingAsync("Theme", Theme);
                await _userSettingsService.SetSettingAsync("EnableAnimations", EnableAnimations);
                await _userSettingsService.SetSettingAsync("FontSize", FontSize);

                // Save route planning settings
                await _userSettingsService.SetSettingAsync("EnableRouteOptimization", EnableRouteOptimization);
                await _userSettingsService.SetSettingAsync("DefaultStopDuration", DefaultStopDuration);
                await _userSettingsService.SetSettingAsync("AutoAssignStudents", AutoAssignStudents);

                // Save data management settings
                await _userSettingsService.SetSettingAsync("EnableDataBackup", EnableDataBackup);
                await _userSettingsService.SetSettingAsync("BackupFrequencyDays", BackupFrequencyDays);
                await _userSettingsService.SetSettingAsync("EnableDataValidation", EnableDataValidation);

                // Save notification settings
                await _userSettingsService.SetSettingAsync("EnableSystemNotifications", EnableSystemNotifications);
                await _userSettingsService.SetSettingAsync("EnableEmailNotifications", EnableEmailNotifications);
                await _userSettingsService.SetSettingAsync("NotifyOnRouteCompletion", NotifyOnRouteCompletion);

                // Save to file
                var success = await _userSettingsService.SaveSettingsAsync();
                if (success)
                {
                    StatusMessage = "Settings saved successfully";
                    Logger.Information("Settings saved successfully");
                    MessageBox.Show("Settings saved successfully!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    throw new InvalidOperationException("Failed to save settings to file");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save settings");
                StatusMessage = $"Failed to save settings: {ex.Message}";
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResetSettingsAsync()
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to reset all settings to their default values?",
                    "Reset Settings",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    StatusMessage = "Resetting settings...";

                    Logger.Information("Resetting user settings to defaults");

                    var success = await _userSettingsService.ResetSettingsAsync();
                    if (success)
                    {
                        // Reload settings to refresh UI
                        await LoadSettingsAsync();
                        StatusMessage = "Settings reset to defaults";
                        Logger.Information("Settings reset to defaults successfully");
                        MessageBox.Show("Settings have been reset to their default values.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to reset settings");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to reset settings");
                StatusMessage = $"Failed to reset settings: {ex.Message}";
                MessageBox.Show($"Failed to reset settings: {ex.Message}", "Settings Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
}
