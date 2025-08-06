using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using BusBuddy.WPF.Services;
using BusBuddy.Core;
using BusBuddy.Core.Data;
using BusBuddy.WPF;
using CoreStudent = BusBuddy.Core.Models.Student;

namespace BusBuddy.WPF.ViewModels;

/// <summary>
/// ViewModel for Students management — MVP phase implementation
/// </summary>
public class StudentsViewModel : INotifyPropertyChanged
{
    private readonly AddressService _addressService;
    private ObservableCollection<CoreStudent> _students = new();
    private CoreStudent? _selectedStudent;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public StudentsViewModel()
    {
        _addressService = new AddressService();
        LoadStudentsCommand = new RelayCommand(async () => await LoadStudentsAsync());
        AddStudentCommand = new RelayCommand(() => AddNewStudent());
        SaveStudentCommand = new RelayCommand(async () => await SaveStudentAsync(), () => CanSaveStudent());
        DeleteStudentCommand = new RelayCommand(async () => await DeleteStudentAsync(), () => CanDeleteStudent());
        ValidateAddressCommand = new RelayCommand(() => ValidateCurrentAddress());
    }

    #region Properties

    public ObservableCollection<CoreStudent> Students
    {
        get => _students;
        set => SetProperty(ref _students, value);
    }

    public CoreStudent? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            if (SetProperty(ref _selectedStudent, value))
            {
                OnPropertyChanged(nameof(IsStudentSelected));
            }
        }
    }

    public bool IsStudentSelected => SelectedStudent != null;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    #endregion

    #region Commands

    public ICommand LoadStudentsCommand { get; }
    public ICommand AddStudentCommand { get; }
    public ICommand SaveStudentCommand { get; }
    public ICommand DeleteStudentCommand { get; }
    public ICommand ValidateAddressCommand { get; }

    #endregion

    #region Methods

    public async Task LoadStudentsAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading students...";

            using var context = new BusBuddyDbContext();
            var students = await context.Students
                .OrderBy(s => s.StudentName)
                .ToListAsync();

            Students.Clear();
            foreach (var student in students)
            {
                Students.Add(student);
            }

            StatusMessage = $"Loaded {students.Count} students";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading students: {ex.Message}";
            // TODO: Add proper logging
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddNewStudent()
    {
        var newStudent = new CoreStudent
        {
            StudentName = "New Student",
            Grade = "K",
            // Initialize with empty values
        };

        Students.Add(newStudent);
        SelectedStudent = newStudent;
        StatusMessage = "New student added. Please edit details and save.";
    }

    public async Task SaveStudentAsync()
    {
        if (SelectedStudent == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Saving student...";

            // Validate address if provided
            if (!string.IsNullOrWhiteSpace(SelectedStudent.HomeAddress))
            {
                var validation = _addressService.ValidateAddress(SelectedStudent.HomeAddress);
                if (!validation.IsValid)
                {
                    StatusMessage = $"Address validation failed: {validation.Error}";
                    return;
                }
            }

            using var context = new BusBuddyDbContext();

            if (SelectedStudent.StudentId == 0)
            {
                // New student
                context.Students.Add(SelectedStudent);
            }
            else
            {
                // Update existing
                context.Students.Update(SelectedStudent);
            }

            await context.SaveChangesAsync();
            StatusMessage = $"Student '{SelectedStudent.StudentName}' saved successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving student: {ex.Message}";
            // TODO: Add proper logging
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteStudentAsync()
    {
        if (SelectedStudent == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Deleting student...";

            using var context = new BusBuddyDbContext();
            var studentToDelete = await context.Students
                .FirstOrDefaultAsync(s => s.StudentId == SelectedStudent.StudentId);

            if (studentToDelete != null)
            {
                context.Students.Remove(studentToDelete);
                await context.SaveChangesAsync();

                Students.Remove(SelectedStudent);
                StatusMessage = $"Student '{studentToDelete.StudentName}' deleted successfully";
                SelectedStudent = null;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting student: {ex.Message}";
            // TODO: Add proper logging
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ValidateCurrentAddress()
    {
        if (SelectedStudent?.HomeAddress == null)
        {
            StatusMessage = "No address to validate";
            return;
        }

        var validation = _addressService.ValidateAddress(SelectedStudent.HomeAddress);
        StatusMessage = validation.IsValid
            ? "Address format is valid"
            : $"Address validation failed: {validation.Error}";
    }

    private bool CanSaveStudent()
    {
        return SelectedStudent != null && !IsLoading;
    }

    private bool CanDeleteStudent()
    {
        return SelectedStudent != null && SelectedStudent.StudentId > 0 && !IsLoading;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
