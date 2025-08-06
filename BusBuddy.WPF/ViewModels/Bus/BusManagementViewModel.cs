using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Views = BusBuddy.WPF.Views;

namespace BusBuddy.WPF.ViewModels.BusManagement
{
    public partial class BusManagementViewModel : ObservableObject
    {
        private readonly IBusService _busService;


        [ObservableProperty]
        private ObservableCollection<BusBuddy.Core.Models.Bus> _buses;

        [ObservableProperty]
        private BusBuddy.Core.Models.Bus _selectedBus;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _pageSize = 20;

        [ObservableProperty]
        private int _totalPages;

        [ObservableProperty]
        private int _totalRecords;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _sortColumn = "BusNumber";

        [ObservableProperty]
        private bool _isSortAscending = true;

        public IAsyncRelayCommand LoadBusesCommand { get; }
        public IAsyncRelayCommand AddBusCommand { get; }
        public IAsyncRelayCommand UpdateBusCommand { get; }
        public IAsyncRelayCommand DeleteBusCommand { get; }
        public IAsyncRelayCommand NextPageCommand { get; }
        public IAsyncRelayCommand PreviousPageCommand { get; }
        public IAsyncRelayCommand FirstPageCommand { get; }
        public IAsyncRelayCommand LastPageCommand { get; }
        public IAsyncRelayCommand<string> SortCommand { get; }

        public BusManagementViewModel(IBusService busService)
        {
            _busService = busService;
            _buses = new ObservableCollection<BusBuddy.Core.Models.Bus>();
            _selectedBus = new BusBuddy.Core.Models.Bus();

            LoadBusesCommand = new AsyncRelayCommand(LoadBusesAsync);
            AddBusCommand = new AsyncRelayCommand(AddBusAsync);
            UpdateBusCommand = new AsyncRelayCommand(UpdateBusAsync, CanUpdateOrDelete);
            DeleteBusCommand = new AsyncRelayCommand(DeleteBusAsync, CanUpdateOrDelete);

            // Pagination commands
            NextPageCommand = new AsyncRelayCommand(NextPageAsync, CanGoToNextPage);
            PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, CanGoToPreviousPage);
            FirstPageCommand = new AsyncRelayCommand(GoToFirstPageAsync, CanGoToPreviousPage);
            LastPageCommand = new AsyncRelayCommand(GoToLastPageAsync, CanGoToNextPage);

            // Sorting command
            SortCommand = new AsyncRelayCommand<string>(SortDataAsync);

            _ = LoadBusesAsync();
        }

        private async Task LoadBusesAsync()
        {
            try
            {
                IsBusy = true;

                // Use the paginated method
                var result = new
                {
                    Buses = await _busService.GetAllBusesAsync(),
                    TotalCount = 0
                };

                // Update the collection
                Buses.Clear();
                foreach (var b in result.Buses)
                {
                    Buses.Add(b);
                }

                // Update pagination information

                TotalRecords = result.TotalCount;
                TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

                // Ensure currentPage is valid
                if (CurrentPage > TotalPages && TotalPages > 0)
                {
                    CurrentPage = TotalPages;
                    await LoadBusesAsync();
                }
            }
            catch (Exception)
            {
                // Handle any exceptions
                // Could display error message to user
            }
            finally
            {
                IsBusy = false;

                // Update command states
                (NextPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (PreviousPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (FirstPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
                (LastPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        private async Task SortDataAsync(string? column)
        {
            if (column == SortColumn)
            {
                // Toggle sort direction if same column
                IsSortAscending = !IsSortAscending;
            }
            else if (column != null)
            {
                // New column, default to ascending
                SortColumn = column;
                IsSortAscending = true;
            }

            await LoadBusesAsync();
        }

        private async Task NextPageAsync()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                await LoadBusesAsync();
            }
        }

        private async Task PreviousPageAsync()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                await LoadBusesAsync();
            }
        }

        private async Task GoToFirstPageAsync()
        {
            CurrentPage = 1;
            await LoadBusesAsync();
        }

        private async Task GoToLastPageAsync()
        {
            CurrentPage = TotalPages;
            await LoadBusesAsync();
        }

        private bool CanGoToNextPage() => CurrentPage < TotalPages && !IsBusy;

        private bool CanGoToPreviousPage() => CurrentPage > 1 && !IsBusy;

        private async Task AddBusAsync()
        {
            IsBusy = true;
            try
            {
                // Create a new bus instance
                var newBus = new BusBuddy.Core.Models.Bus
                {
                    Status = "Active",
                    Year = DateTime.Now.Year
                };

                // Show the edit dialog
                var dialog = new Views.Bus.BusEditDialog(newBus);
                dialog.Owner = Application.Current.MainWindow;

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    // Add the new bus to the database
                    var addedBus = await _busService.AddBusAsync(dialog.Bus);

                    // Reload the data
                    await LoadBusesAsync();

                    // Select the newly added bus
                    var busInList = Buses.FirstOrDefault(b => b.VehicleId == addedBus.VehicleId);
                    if (busInList != null)
                    {
                        SelectedBus = busInList;
                    }

                    // Show success notification
                    var notification = new Views.Bus.NotificationWindow(
                        $"Bus #{dialog.Bus.BusNumber} was successfully added.",
                        "Bus Added",
                        Views.Bus.NotificationWindow.NotificationType.Success);
                    notification.Owner = Application.Current.MainWindow;
                    notification.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                // Show error notification
                var notification = new Views.Bus.NotificationWindow(
                    $"Failed to add bus: {ex.Message}",
                    "Error",
                    Views.Bus.NotificationWindow.NotificationType.Error);
                notification.Owner = Application.Current.MainWindow;
                notification.ShowDialog();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task UpdateBusAsync()
        {
            if (SelectedBus == null)
            {
                // Show notification to select a bus first
                var notification = new Views.Bus.NotificationWindow(
                    "Please select a bus to update.",
                    "No Bus Selected",
                    Views.Bus.NotificationWindow.NotificationType.Warning);
                notification.Owner = Application.Current.MainWindow;
                notification.ShowDialog();
                return;
            }

            IsBusy = true;
            try
            {
                // Create a clone for editing
                var busToEdit = SelectedBus;

                // Show the edit dialog
                var dialog = new Views.Bus.BusEditDialog(busToEdit);
                dialog.Owner = Application.Current.MainWindow;

                var result = dialog.ShowDialog();

                if (result == true)
                {
                    // Update the bus in the database
                    await _busService.UpdateBusAsync(dialog.Bus);

                    // Reload the data
                    await LoadBusesAsync();

                    // Try to re-select the same bus
                    var updatedBus = Buses.FirstOrDefault(b => b.VehicleId == busToEdit.VehicleId);
                    if (updatedBus != null)
                    {
                        SelectedBus = updatedBus;
                    }

                    // Show success notification
                    var notification = new Views.Bus.NotificationWindow(
                        $"Bus #{dialog.Bus.BusNumber} was successfully updated.",
                        "Bus Updated",
                        Views.Bus.NotificationWindow.NotificationType.Success);
                    notification.Owner = Application.Current.MainWindow;
                    notification.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                // Show error notification
                var notification = new Views.Bus.NotificationWindow(
                    $"Failed to update bus: {ex.Message}",
                    "Error",
                    Views.Bus.NotificationWindow.NotificationType.Error);
                notification.Owner = Application.Current.MainWindow;
                notification.ShowDialog();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteBusAsync()
        {
            if (SelectedBus == null)
            {
                // Show notification to select a bus first
                var notification = new Views.Bus.NotificationWindow(
                    "Please select a bus to delete.",
                    "No Bus Selected",
                    Views.Bus.NotificationWindow.NotificationType.Warning);
                notification.Owner = Application.Current.MainWindow;
                notification.ShowDialog();
                return;
            }

            // Show confirmation dialog
            var busNumber = SelectedBus.BusNumber;
            var busId = SelectedBus.VehicleId;

            var confirmDialog = new Views.Bus.ConfirmationDialog(
                $"Are you sure you want to delete Bus #{busNumber}? This action cannot be undone.",
                "Confirm Delete");
            confirmDialog.Owner = Application.Current.MainWindow;

            var result = confirmDialog.ShowDialog();

            if (result == true)
            {
                IsBusy = true;
                try
                {
                    // Delete the bus from the database
                    await _busService.DeleteBusAsync(busId);

                    // Reload the data
                    await LoadBusesAsync();

                    // Show success notification
                    var notification = new Views.Bus.NotificationWindow(
                        $"Bus #{busNumber} was successfully deleted.",
                        "Bus Deleted",
                        Views.Bus.NotificationWindow.NotificationType.Success);
                    notification.Owner = Application.Current.MainWindow;
                    notification.ShowDialog();
                }
                catch (Exception ex)
                {
                    // Show error notification
                    var notification = new Views.Bus.NotificationWindow(
                        $"Failed to delete bus: {ex.Message}",
                        "Error",
                        Views.Bus.NotificationWindow.NotificationType.Error);
                    notification.Owner = Application.Current.MainWindow;
                    notification.ShowDialog();
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private bool CanUpdateOrDelete()
        {
            return SelectedBus != null && SelectedBus.VehicleId != 0 && !IsBusy;
        }

        partial void OnSelectedBusChanged(BusBuddy.Core.Models.Bus value)
        {
            (UpdateBusCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (DeleteBusCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        }

        partial void OnCurrentPageChanged(int value)
        {
            (NextPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (PreviousPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (FirstPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
            (LastPageCommand as IRelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}
