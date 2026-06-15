using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Collections.Generic;

namespace BusBuddy.WPF.ViewModels
{
    public class ActivityLoggingViewModel : BaseInDevelopmentViewModel
    {
        private readonly IActivityLogService _logService;
        private ObservableCollection<ActivityLog> _logs = new();
        private DateTime _startDate = DateTime.Now.AddDays(-30);
        private DateTime _endDate = DateTime.Now;
        private string _searchText = string.Empty;
        private ActivityLog? _selectedLog;
        private readonly int _defaultLogLimit = 1000;

        public ObservableCollection<ActivityLog> Logs
        {
            get => _logs;
            set => SetProperty(ref _logs, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    _ = RefreshLogsAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    _ = RefreshLogsAsync();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = RefreshLogsAsync();
                }
            }
        }

        public ActivityLog? SelectedLog
        {
            get => _selectedLog;
            set => SetProperty(ref _selectedLog, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand ViewDetailsCommand { get; }

        public ActivityLoggingViewModel(IActivityLogService logService)
            : base()
        {
            _logService = logService;

            RefreshCommand = new RelayCommand(async () => await RefreshLogsAsync());
            ExportCommand = new RelayCommand(async () => await ExportLogsAsync());
            ClearFiltersCommand = new RelayCommand(() => ClearFilters());
            ViewDetailsCommand = new RelayCommand(() => ViewLogDetails(), () => SelectedLog != null);

            // Set development status to false to make the view fully functional
            IsInDevelopment = false;

            // Load logs when the view model is created
            _ = RefreshLogsAsync();
        }

        private async Task RefreshLogsAsync()
        {
            try
            {
                IsLoading = true;
                Logger.Debug("Refreshing activity logs with filter: Start={StartDate}, End={EndDate}, Search={SearchText}",
                    StartDate, EndDate, SearchText);

                // Use the optimized date range method if dates are specified
                if (StartDate.Date != DateTime.Now.AddDays(-30).Date || EndDate.Date != DateTime.Now.Date)
                {
                    var allLogs = await _logService.GetLogsByDateRangeAsync(StartDate, EndDate, _defaultLogLimit);

                    // Apply search filter in memory (smaller dataset after date filter)
                    var filteredLogs = allLogs
                        .Where(log => string.IsNullOrWhiteSpace(SearchText) ||
                                     log.Action.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     log.User.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     (log.Details != null && log.Details.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    Logs.Clear();
                    foreach (var log in filteredLogs)
                    {
                        Logs.Add(log);
                    }
                }
                else
                {
                    // Use paged method for recent logs
                    var allLogs = await _logService.GetLogsPagedAsync(1, _defaultLogLimit);

                    // Apply search filter in memory
                    var filteredLogs = allLogs
                        .Where(log => string.IsNullOrWhiteSpace(SearchText) ||
                                     log.Action.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     log.User.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                     (log.Details != null && log.Details.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    Logs.Clear();
                    foreach (var log in filteredLogs)
                    {
                        Logs.Add(log);
                    }
                }

                Logger.Information("Loaded {FilteredCount} activity logs after filtering", Logs.Count);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error loading activity logs");
                MessageBox.Show($"Error loading activity logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportLogsAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    FileName = $"ActivityLogs_{DateTime.Now:yyyyMMdd}",
                    DefaultExt = ".csv",
                    Filter = "CSV Files (*.csv)|*.csv"
                };

                var result = dialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    Logger.Information("Exporting {LogCount} activity logs to {FilePath}", Logs.Count, dialog.FileName);

                    using var writer = new StreamWriter(dialog.FileName);

                    // Write header
                    writer.WriteLine("ID,Timestamp,Action,User,Details");

                    // Write data
                    foreach (var log in Logs)
                    {
                        writer.WriteLine(
                            $"{log.Id}," +
                            $"{log.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                            $"\"{log.Action.Replace("\"", "\"\"")}\"," +
                            $"\"{log.User.Replace("\"", "\"\"")}\"," +
                            $"\"{(log.Details?.Replace("\"", "\"\"") ?? "")}\"");
                    }

                    await writer.FlushAsync();

                    MessageBox.Show($"Successfully exported {Logs.Count} activity logs to {dialog.FileName}",
                        "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error exporting activity logs");
                MessageBox.Show($"Error exporting activity logs: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearFilters()
        {
            StartDate = DateTime.Now.AddDays(-30);
            EndDate = DateTime.Now;
            SearchText = string.Empty;
        }

        private void ViewLogDetails()
        {
            if (SelectedLog == null)
            {
                return;
            }


            var details = $"Log ID: {SelectedLog.Id}\n" +
                         $"Timestamp: {SelectedLog.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                         $"Action: {SelectedLog.Action}\n" +
                         $"User: {SelectedLog.User}\n\n" +
                         $"Details:\n{SelectedLog.Details ?? "No details provided"}";

            MessageBox.Show(details, "Activity Log Details", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
