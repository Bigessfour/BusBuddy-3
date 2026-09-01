using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BusBuddy.WPF.ViewModels.Student;

/// <summary>School-to-school transfer dialog — pickup/dropoff locations and times required.</summary>
public sealed class StudentSchoolTransferViewModel : BaseViewModel
{
    private static readonly new ILogger Logger = Log.ForContext<StudentSchoolTransferViewModel>();

    private readonly IStudentSchoolTransferService _transferService;
    private readonly IDestinationService _destinationService;
    private readonly int _studentId;
    private readonly string _studentName;

    private Destination? _fromSchool;
    private Destination? _toSchool;
    private string _pickupAddress = string.Empty;
    private string _dropoffAddress = string.Empty;
    private string _pickupTimeText = "07:15";
    private string _dropoffTimeText = "07:45";
    private string _notes = string.Empty;
    private string _validationMessage = string.Empty;

    public event EventHandler<bool?>? RequestClose;

    public StudentSchoolTransferViewModel(
        int studentId,
        string studentName,
        IStudentSchoolTransferService transferService,
        IDestinationService destinationService)
    {
        _studentId = studentId;
        _studentName = studentName;
        _transferService = transferService ?? throw new ArgumentNullException(nameof(transferService));
        _destinationService = destinationService ?? throw new ArgumentNullException(nameof(destinationService));

        Schools = new ObservableCollection<Destination>();
        // Always executable — validate inside SaveAsync so ButtonAdv never silently no-ops.
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, false));
        _ = LoadSchoolsAsync();
    }

    public string Title => $"School Transfer — {_studentName}";

    public ObservableCollection<Destination> Schools { get; }

    public Destination? FromSchool
    {
        get => _fromSchool;
        set
        {
            if (SetProperty(ref _fromSchool, value))
            {
                if (value is not null && string.IsNullOrWhiteSpace(PickupAddress))
                {
                    PickupAddress = value.FullAddress;
                }

                NotifySave();
            }
        }
    }

    public Destination? ToSchool
    {
        get => _toSchool;
        set
        {
            if (SetProperty(ref _toSchool, value))
            {
                if (value is not null && string.IsNullOrWhiteSpace(DropoffAddress))
                {
                    DropoffAddress = value.FullAddress;
                }

                NotifySave();
            }
        }
    }

    public string PickupAddress
    {
        get => _pickupAddress;
        set
        {
            if (SetProperty(ref _pickupAddress, value))
            {
                NotifySave();
            }
        }
    }

    public string DropoffAddress
    {
        get => _dropoffAddress;
        set
        {
            if (SetProperty(ref _dropoffAddress, value))
            {
                NotifySave();
            }
        }
    }

    public string PickupTimeText
    {
        get => _pickupTimeText;
        set
        {
            if (SetProperty(ref _pickupTimeText, value))
            {
                NotifySave();
            }
        }
    }

    public string DropoffTimeText
    {
        get => _dropoffTimeText;
        set
        {
            if (SetProperty(ref _dropoffTimeText, value))
            {
                NotifySave();
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool CanSave =>
        FromSchool is not null &&
        ToSchool is not null &&
        FromSchool.DestinationId != ToSchool.DestinationId &&
        !string.IsNullOrWhiteSpace(PickupAddress) &&
        !string.IsNullOrWhiteSpace(DropoffAddress) &&
        TryParseTime(PickupTimeText, out _) &&
        TryParseTime(DropoffTimeText, out var drop) &&
        TryParseTime(PickupTimeText, out var pick) &&
        drop > pick;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    private async Task LoadSchoolsAsync()
    {
        try
        {
            IsLoading = true;
            var schools = await _destinationService.GetActiveSchoolsAsync();
            Schools.Clear();
            foreach (var s in schools)
            {
                Schools.Add(s);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed loading schools for transfer");
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveAsync()
    {
        ValidationMessage = string.Empty;
        if (!CanSave || FromSchool is null || ToSchool is null)
        {
            ValidationMessage = "From/To schools, pickup & dropoff locations, and times (dropoff after pickup) are required.";
            return;
        }

        if (!TryParseTime(PickupTimeText, out var pickup) || !TryParseTime(DropoffTimeText, out var dropoff))
        {
            ValidationMessage = "Use time format HH:mm (e.g. 07:15).";
            return;
        }

        try
        {
            IsLoading = true;
            await _transferService.AssignTransferAsync(new StudentSchoolTransfer
            {
                StudentId = _studentId,
                FromDestinationId = FromSchool.DestinationId,
                ToDestinationId = ToSchool.DestinationId,
                PickupAddress = PickupAddress.Trim(),
                DropoffAddress = DropoffAddress.Trim(),
                PickupTime = pickup,
                DropoffTime = dropoff,
                EffectiveDate = DateTime.Today,
                Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                CreatedBy = Environment.UserName
            });
            StatusMessage = "Transfer saved";
            RequestClose?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed assigning school transfer");
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void NotifySave()
    {
        OnPropertyChanged(nameof(CanSave));
        if (SaveCommand is IRelayCommand relay)
        {
            relay.NotifyCanExecuteChanged();
        }
    }

    private static bool TryParseTime(string? text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // SfMaskedEdit may leave prompt chars (e.g. "07:1_") — strip before parse.
        var normalized = text
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(normalized) || !normalized.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        return TimeSpan.TryParseExact(
                   normalized,
                   new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" },
                   CultureInfo.InvariantCulture,
                   out time)
               || TimeSpan.TryParse(normalized, CultureInfo.InvariantCulture, out time);
    }
}
