# 🚨 Error Handling - Serilog & Exception Management Patterns

**Part of BusBuddy Copilot Reference Hub**  
**Last Updated**: August 3, 2025  
**Purpose**: Provide GitHub Copilot with comprehensive error handling patterns using Serilog and structured exception management

---

## 🏗️ **Serilog Configuration Patterns**

### **Startup Configuration (App.xaml.cs)**
```csharp
// BusBuddy.WPF/App.xaml.cs
using Serilog;
using Serilog.Events;
using Serilog.Enrichers;

public partial class App : Application
{
    private static readonly ILogger Logger = Log.ForContext<App>();

    public App()
    {
        // Configure Serilog BEFORE any other initialization
        ConfigureSerilog();
        
        // Register Syncfusion license
        RegisterSyncfusionLicense();
        
        // Handle unhandled exceptions
        SetupGlobalExceptionHandling();
    }

    private void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithProperty("Application", "BusBuddy")
            .Enrich.WithProperty("Version", GetAppVersion())
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine("logs", "busbuddy-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine("logs", "busbuddy-errors-.log"),
                restrictedToMinimumLevel: LogEventLevel.Warning,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        Logger.Information("BusBuddy application starting - Serilog configured");
        Logger.Information("Log files location: {LogPath}", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
    }

    private void SetupGlobalExceptionHandling()
    {
        // Handle unhandled exceptions in UI thread
        DispatcherUnhandledException += (sender, e) =>
        {
            Logger.Fatal(e.Exception, "Unhandled exception in UI thread: {ErrorMessage}", e.Exception.Message);
            
            var result = MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nWould you like to continue?",
                "Application Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            e.Handled = result == MessageBoxResult.Yes;
        };

        // Handle unhandled exceptions in background threads
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var exception = e.ExceptionObject as Exception;
            Logger.Fatal(exception, "Unhandled exception in background thread: {ErrorMessage}", exception?.Message);
            
            MessageBox.Show(
                $"A critical error occurred:\n\n{exception?.Message}\n\nThe application will now close.",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        // Handle unhandled task exceptions
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Logger.Error(e.Exception, "Unobserved task exception: {ErrorMessage}", e.Exception.Message);
            e.SetObserved(); // Prevent process termination
        };
    }

    private string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "Unknown";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Information("BusBuddy application exiting with code {ExitCode}", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
```

### **Service Layer Error Patterns**
```csharp
// BusBuddy.Core/Services/StudentService.cs
using Serilog;

public class StudentService : IStudentService
{
    private static readonly ILogger Logger = Log.ForContext<StudentService>();
    private readonly IStudentRepository _studentRepository;

    public StudentService(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository ?? throw new ArgumentNullException(nameof(studentRepository));
    }

    public async Task<ServiceResult<Student>> CreateStudentAsync(CreateStudentRequest request)
    {
        using var activity = Logger.BeginScope("CreateStudent");
        Logger.Information("Creating new student: {FirstName} {LastName}", request.FirstName, request.LastName);

        try
        {
            // Input validation with structured logging
            var validationResult = ValidateCreateStudentRequest(request);
            if (!validationResult.IsSuccess)
            {
                Logger.Warning("Student creation failed validation: {ValidationErrors}", 
                    string.Join(", ", validationResult.Errors));
                return ServiceResult<Student>.Failure(validationResult.Errors);
            }

            // Check for duplicates
            var exists = await _studentRepository.StudentExistsAsync(
                request.FirstName, request.LastName, request.DateOfBirth);
            
            if (exists)
            {
                var errorMessage = $"Student already exists: {request.FirstName} {request.LastName} ({request.DateOfBirth:yyyy-MM-dd})";
                Logger.Warning("Duplicate student creation attempt: {FirstName} {LastName} {DateOfBirth}", 
                    request.FirstName, request.LastName, request.DateOfBirth);
                return ServiceResult<Student>.Failure(errorMessage);
            }

            // Create student entity
            var student = new Student
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Grade = request.Grade,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                City = request.City,
                ZipCode = request.ZipCode,
                EmergencyContact = request.EmergencyContact,
                EmergencyContactName = request.EmergencyContactName,
                MedicalNotes = request.MedicalNotes
            };

            // Save to database with detailed logging
            var savedStudent = await _studentRepository.AddAsync(student);
            
            Logger.Information("Successfully created student {StudentId}: {FullName} in Grade {Grade}",
                savedStudent.StudentId, savedStudent.FullName, savedStudent.Grade);

            return ServiceResult<Student>.Success(savedStudent);
        }
        catch (DbUpdateException ex)
        {
            Logger.Error(ex, "Database error creating student {FirstName} {LastName}: {InnerException}",
                request.FirstName, request.LastName, ex.InnerException?.Message);
            return ServiceResult<Student>.Failure("Database error occurred while creating student. Please try again.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error creating student {FirstName} {LastName}: {ErrorMessage}",
                request.FirstName, request.LastName, ex.Message);
            return ServiceResult<Student>.Failure("An unexpected error occurred. Please contact support.");
        }
    }

    public async Task<ServiceResult<List<Student>>> GetStudentsByGradeAsync(int grade)
    {
        using var activity = Logger.BeginScope("GetStudentsByGrade");
        Logger.Debug("Retrieving students for grade {Grade}", grade);

        try
        {
            if (grade < 0 || grade > 12)
            {
                Logger.Warning("Invalid grade requested: {Grade}", grade);
                return ServiceResult<List<Student>>.Failure("Grade must be between 0 and 12");
            }

            var students = await _studentRepository.GetStudentsByGradeAsync(grade);
            
            Logger.Information("Retrieved {StudentCount} students for grade {Grade}", students.Count, grade);
            return ServiceResult<List<Student>>.Success(students);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error retrieving students for grade {Grade}: {ErrorMessage}", grade, ex.Message);
            return ServiceResult<List<Student>>.Failure("Error retrieving students. Please try again.");
        }
    }

    private ValidationResult ValidateCreateStudentRequest(CreateStudentRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FirstName))
            errors.Add("First name is required");
        else if (request.FirstName.Length > 50)
            errors.Add("First name cannot exceed 50 characters");

        if (string.IsNullOrWhiteSpace(request.LastName))
            errors.Add("Last name is required");
        else if (request.LastName.Length > 50)
            errors.Add("Last name cannot exceed 50 characters");

        if (request.Grade < 0 || request.Grade > 12)
            errors.Add("Grade must be between 0 and 12");

        if (request.DateOfBirth == default || request.DateOfBirth > DateTime.Today)
            errors.Add("Valid date of birth is required");

        if (request.DateOfBirth < DateTime.Today.AddYears(-25))
            errors.Add("Student cannot be older than 25 years");

        return errors.Any() ? ValidationResult.Failure(errors) : ValidationResult.Success();
    }
}
```

---

## 🖥️ **ViewModel Error Handling**

### **Base ViewModel with Error Management**
```csharp
// BusBuddy.WPF/ViewModels/BaseViewModel.cs
using Serilog;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    private static readonly ILogger Logger = Log.ForContext<BaseViewModel>();
    private bool _isLoading;
    private string? _errorMessage;
    private string? _successMessage;

    protected BaseViewModel()
    {
        Logger.Debug("Creating {ViewModelType}", GetType().Name);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                Logger.Debug("{ViewModelType} loading state changed to {IsLoading}", GetType().Name, value);
                OnPropertyChanged(nameof(HasNoErrors));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Logger.Warning("{ViewModelType} error: {ErrorMessage}", GetType().Name, value);
                }
                OnPropertyChanged(nameof(HasErrors));
                OnPropertyChanged(nameof(HasNoErrors));
            }
        }
    }

    public string? SuccessMessage
    {
        get => _successMessage;
        set
        {
            if (SetProperty(ref _successMessage, value))
            {
                if (!string.IsNullOrEmpty(value))
                {
                    Logger.Information("{ViewModelType} success: {SuccessMessage}", GetType().Name, value);
                }
                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasNoErrors => !HasErrors && !IsLoading;
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    protected virtual async Task ExecuteWithErrorHandlingAsync(Func<Task> operation, string operationName)
    {
        using var activity = Logger.BeginScope("ExecuteOperation");
        Logger.Debug("Executing {OperationName} in {ViewModelType}", operationName, GetType().Name);

        try
        {
            ClearMessages();
            IsLoading = true;

            await operation();

            Logger.Debug("Successfully completed {OperationName} in {ViewModelType}", operationName, GetType().Name);
        }
        catch (ServiceException ex)
        {
            ErrorMessage = ex.Message;
            Logger.Warning("Service error in {OperationName}: {ErrorMessage}", operationName, ex.Message);
        }
        catch (ValidationException ex)
        {
            ErrorMessage = $"Validation error: {ex.Message}";
            Logger.Warning("Validation error in {OperationName}: {ErrorMessage}", operationName, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            ErrorMessage = "Database error occurred. Please try again.";
            Logger.Error(ex, "Database error in {OperationName}: {InnerException}", operationName, ex.InnerException?.Message);
        }
        catch (Exception ex)
        {
            ErrorMessage = "An unexpected error occurred. Please contact support.";
            Logger.Error(ex, "Unexpected error in {OperationName}: {ErrorMessage}", operationName, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected virtual async Task<T?> ExecuteWithErrorHandlingAsync<T>(Func<Task<T>> operation, string operationName)
    {
        using var activity = Logger.BeginScope("ExecuteOperation");
        Logger.Debug("Executing {OperationName} in {ViewModelType}", operationName, GetType().Name);

        try
        {
            ClearMessages();
            IsLoading = true;

            var result = await operation();
            Logger.Debug("Successfully completed {OperationName} in {ViewModelType}", operationName, GetType().Name);
            return result;
        }
        catch (ServiceException ex)
        {
            ErrorMessage = ex.Message;
            Logger.Warning("Service error in {OperationName}: {ErrorMessage}", operationName, ex.Message);
        }
        catch (ValidationException ex)
        {
            ErrorMessage = $"Validation error: {ex.Message}";
            Logger.Warning("Validation error in {OperationName}: {ErrorMessage}", operationName, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            ErrorMessage = "Database error occurred. Please try again.";
            Logger.Error(ex, "Database error in {OperationName}: {InnerException}", operationName, ex.InnerException?.Message);
        }
        catch (Exception ex)
        {
            ErrorMessage = "An unexpected error occurred. Please contact support.";
            Logger.Error(ex, "Unexpected error in {OperationName}: {ErrorMessage}", operationName, ex.Message);
        }
        finally
        {
            IsLoading = false;
        }

        return default;
    }

    protected void ShowError(string message)
    {
        ErrorMessage = message;
        Logger.Warning("{ViewModelType} showing error: {ErrorMessage}", GetType().Name, message);
    }

    protected void ShowSuccess(string message)
    {
        SuccessMessage = message;
        Logger.Information("{ViewModelType} showing success: {SuccessMessage}", GetType().Name, message);
    }

    protected void ClearMessages()
    {
        ErrorMessage = null;
        SuccessMessage = null;
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

### **Student Management ViewModel with Error Handling**
```csharp
// BusBuddy.WPF/ViewModels/StudentManagementViewModel.cs
public class StudentManagementViewModel : BaseViewModel
{
    private static readonly ILogger Logger = Log.ForContext<StudentManagementViewModel>();
    private readonly IStudentService _studentService;
    private ObservableCollection<Student> _students = new();
    private Student? _selectedStudent;
    private string _searchText = string.Empty;

    public StudentManagementViewModel(IStudentService studentService)
    {
        _studentService = studentService ?? throw new ArgumentNullException(nameof(studentService));
        
        LoadStudentsCommand = new AsyncRelayCommand(LoadStudentsAsync);
        AddStudentCommand = new AsyncRelayCommand(AddStudentAsync);
        EditStudentCommand = new AsyncRelayCommand<Student>(EditStudentAsync, CanEditStudent);
        DeleteStudentCommand = new AsyncRelayCommand<Student>(DeleteStudentAsync, CanDeleteStudent);
        SearchStudentsCommand = new AsyncRelayCommand(SearchStudentsAsync);
        ClearSearchCommand = new RelayCommand(ClearSearch);

        Logger.Information("StudentManagementViewModel initialized");
    }

    public ObservableCollection<Student> Students
    {
        get => _students;
        set => SetProperty(ref _students, value);
    }

    public Student? SelectedStudent
    {
        get => _selectedStudent;
        set
        {
            if (SetProperty(ref _selectedStudent, value))
            {
                EditStudentCommand.NotifyCanExecuteChanged();
                DeleteStudentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public AsyncRelayCommand LoadStudentsCommand { get; }
    public AsyncRelayCommand AddStudentCommand { get; }
    public AsyncRelayCommand<Student> EditStudentCommand { get; }
    public AsyncRelayCommand<Student> DeleteStudentCommand { get; }
    public AsyncRelayCommand SearchStudentsCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

    private async Task LoadStudentsAsync()
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            Logger.Information("Loading all students");
            
            var result = await _studentService.GetAllStudentsAsync();
            if (result.IsSuccess)
            {
                Students.Clear();
                foreach (var student in result.Data!)
                {
                    Students.Add(student);
                }
                
                ShowSuccess($"Loaded {Students.Count} students");
                Logger.Information("Successfully loaded {StudentCount} students", Students.Count);
            }
            else
            {
                ShowError(string.Join(", ", result.Errors));
                Logger.Warning("Failed to load students: {Errors}", string.Join(", ", result.Errors));
            }
        }, "LoadStudents");
    }

    private async Task AddStudentAsync()
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            Logger.Information("Opening student creation dialog");
            
            var dialog = new StudentEntryDialog();
            if (dialog.ShowDialog() == true && dialog.Student != null)
            {
                var request = new CreateStudentRequest
                {
                    FirstName = dialog.Student.FirstName,
                    LastName = dialog.Student.LastName,
                    Grade = dialog.Student.Grade,
                    DateOfBirth = dialog.Student.DateOfBirth,
                    Address = dialog.Student.Address,
                    City = dialog.Student.City,
                    ZipCode = dialog.Student.ZipCode,
                    EmergencyContact = dialog.Student.EmergencyContact,
                    EmergencyContactName = dialog.Student.EmergencyContactName,
                    MedicalNotes = dialog.Student.MedicalNotes
                };

                var result = await _studentService.CreateStudentAsync(request);
                if (result.IsSuccess)
                {
                    Students.Add(result.Data!);
                    ShowSuccess($"Successfully added {result.Data.FullName}");
                    Logger.Information("Successfully added student {StudentId}: {FullName}", 
                        result.Data.StudentId, result.Data.FullName);
                }
                else
                {
                    ShowError(string.Join(", ", result.Errors));
                    Logger.Warning("Failed to add student: {Errors}", string.Join(", ", result.Errors));
                }
            }
        }, "AddStudent");
    }

    private async Task EditStudentAsync(Student? student)
    {
        if (student == null) return;

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            Logger.Information("Opening student edit dialog for {StudentId}: {FullName}", 
                student.StudentId, student.FullName);
            
            var dialog = new StudentEntryDialog(student);
            if (dialog.ShowDialog() == true && dialog.Student != null)
            {
                var request = new UpdateStudentRequest
                {
                    StudentId = student.StudentId,
                    FirstName = dialog.Student.FirstName,
                    LastName = dialog.Student.LastName,
                    Grade = dialog.Student.Grade,
                    DateOfBirth = dialog.Student.DateOfBirth,
                    Address = dialog.Student.Address,
                    City = dialog.Student.City,
                    ZipCode = dialog.Student.ZipCode,
                    EmergencyContact = dialog.Student.EmergencyContact,
                    EmergencyContactName = dialog.Student.EmergencyContactName,
                    MedicalNotes = dialog.Student.MedicalNotes
                };

                var result = await _studentService.UpdateStudentAsync(request);
                if (result.IsSuccess)
                {
                    var index = Students.IndexOf(student);
                    if (index >= 0)
                    {
                        Students[index] = result.Data!;
                    }
                    
                    ShowSuccess($"Successfully updated {result.Data!.FullName}");
                    Logger.Information("Successfully updated student {StudentId}: {FullName}", 
                        result.Data.StudentId, result.Data.FullName);
                }
                else
                {
                    ShowError(string.Join(", ", result.Errors));
                    Logger.Warning("Failed to update student {StudentId}: {Errors}", 
                        student.StudentId, string.Join(", ", result.Errors));
                }
            }
        }, "EditStudent");
    }

    private async Task DeleteStudentAsync(Student? student)
    {
        if (student == null) return;

        var confirmResult = MessageBox.Show(
            $"Are you sure you want to delete {student.FullName}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult != MessageBoxResult.Yes)
        {
            Logger.Debug("User cancelled deletion of student {StudentId}: {FullName}", 
                student.StudentId, student.FullName);
            return;
        }

        await ExecuteWithErrorHandlingAsync(async () =>
        {
            Logger.Warning("Deleting student {StudentId}: {FullName}", student.StudentId, student.FullName);
            
            var result = await _studentService.DeleteStudentAsync(student.StudentId);
            if (result.IsSuccess)
            {
                Students.Remove(student);
                SelectedStudent = null;
                
                ShowSuccess($"Successfully deleted {student.FullName}");
                Logger.Information("Successfully deleted student {StudentId}: {FullName}", 
                    student.StudentId, student.FullName);
            }
            else
            {
                ShowError(string.Join(", ", result.Errors));
                Logger.Error("Failed to delete student {StudentId}: {Errors}", 
                    student.StudentId, string.Join(", ", result.Errors));
            }
        }, "DeleteStudent");
    }

    private async Task SearchStudentsAsync()
    {
        await ExecuteWithErrorHandlingAsync(async () =>
        {
            Logger.Information("Searching students with term: {SearchText}", SearchText);
            
            var result = await _studentService.SearchStudentsAsync(SearchText);
            if (result.IsSuccess)
            {
                Students.Clear();
                foreach (var student in result.Data!)
                {
                    Students.Add(student);
                }
                
                ShowSuccess($"Found {Students.Count} students matching '{SearchText}'");
                Logger.Information("Search returned {StudentCount} students for term: {SearchText}", 
                    Students.Count, SearchText);
            }
            else
            {
                ShowError(string.Join(", ", result.Errors));
                Logger.Warning("Search failed for term '{SearchText}': {Errors}", 
                    SearchText, string.Join(", ", result.Errors));
            }
        }, "SearchStudents");
    }

    private void ClearSearch()
    {
        SearchText = string.Empty;
        LoadStudentsCommand.ExecuteAsync(null);
        Logger.Debug("Cleared search and reloaded all students");
    }

    private bool CanEditStudent(Student? student) => student != null && !IsLoading;
    private bool CanDeleteStudent(Student? student) => student != null && !IsLoading;
}
```

---

## ⚠️ **Custom Exception Types**

### **Service Layer Exceptions**
```csharp
// BusBuddy.Core/Exceptions/ServiceException.cs
public class ServiceException : Exception
{
    public string[] Errors { get; }

    public ServiceException(string message) : base(message)
    {
        Errors = new[] { message };
    }

    public ServiceException(string message, Exception innerException) : base(message, innerException)
    {
        Errors = new[] { message };
    }

    public ServiceException(string[] errors) : base(string.Join(", ", errors))
    {
        Errors = errors;
    }

    public ServiceException(string message, string[] errors) : base(message)
    {
        Errors = errors;
    }
}

// BusBuddy.Core/Exceptions/ValidationException.cs
public class ValidationException : ServiceException
{
    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string[] errors) : base(errors)
    {
    }

    public ValidationException(string message, string[] errors) : base(message, errors)
    {
    }
}

// BusBuddy.Core/Exceptions/EntityNotFoundException.cs
public class EntityNotFoundException : ServiceException
{
    public Type EntityType { get; }
    public object EntityId { get; }

    public EntityNotFoundException(Type entityType, object entityId) 
        : base($"{entityType.Name} with ID '{entityId}' was not found")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    public EntityNotFoundException(string entityName, object entityId) 
        : base($"{entityName} with ID '{entityId}' was not found")
    {
        EntityType = typeof(object);
        EntityId = entityId;
    }
}

// BusBuddy.Core/Exceptions/DuplicateEntityException.cs
public class DuplicateEntityException : ServiceException
{
    public Type EntityType { get; }
    public string DuplicateField { get; }
    public object DuplicateValue { get; }

    public DuplicateEntityException(Type entityType, string field, object value) 
        : base($"{entityType.Name} with {field} '{value}' already exists")
    {
        EntityType = entityType;
        DuplicateField = field;
        DuplicateValue = value;
    }

    public DuplicateEntityException(string entityName, string field, object value) 
        : base($"{entityName} with {field} '{value}' already exists")
    {
        EntityType = typeof(object);
        DuplicateField = field;
        DuplicateValue = value;
    }
}
```

---

## 📊 **Result and Response Patterns**

### **Service Result Pattern**
```csharp
// BusBuddy.Core/Models/ServiceResult.cs
public class ServiceResult
{
    public bool IsSuccess { get; private set; }
    public string[] Errors { get; private set; } = Array.Empty<string>();

    protected ServiceResult(bool isSuccess, string[] errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static ServiceResult Success() => new(true, Array.Empty<string>());
    public static ServiceResult Failure(string error) => new(false, new[] { error });
    public static ServiceResult Failure(string[] errors) => new(false, errors);
    public static ServiceResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }

    private ServiceResult(bool isSuccess, T? data, string[] errors) : base(isSuccess, errors)
    {
        Data = data;
    }

    public static ServiceResult<T> Success(T data) => new(true, data, Array.Empty<string>());
    public static new ServiceResult<T> Failure(string error) => new(false, default, new[] { error });
    public static new ServiceResult<T> Failure(string[] errors) => new(false, default, errors);
    public static new ServiceResult<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToArray());
}

// BusBuddy.Core/Models/ValidationResult.cs
public class ValidationResult
{
    public bool IsSuccess { get; private set; }
    public string[] Errors { get; private set; } = Array.Empty<string>();

    private ValidationResult(bool isSuccess, string[] errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static ValidationResult Success() => new(true, Array.Empty<string>());
    public static ValidationResult Failure(string error) => new(false, new[] { error });
    public static ValidationResult Failure(string[] errors) => new(false, errors);
    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors.ToArray());
}
```

---

## 🎯 **Error Handling in WPF Views**

### **Error Display User Control**
```xml
<!-- BusBuddy.WPF/Controls/ErrorDisplayControl.xaml -->
<UserControl x:Class="BusBuddy.WPF.Controls.ErrorDisplayControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <!-- Error Message -->
        <Border Background="#FFEBEE" BorderBrush="#F44336" BorderThickness="1" 
                CornerRadius="4" Padding="12" Margin="0,0,0,8"
                Visibility="{Binding HasErrors, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="⚠" Foreground="#F44336" FontSize="16" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBlock Text="{Binding ErrorMessage}" Foreground="#C62828" 
                          TextWrapping="Wrap" VerticalAlignment="Center"/>
                <Button Content="✕" Background="Transparent" BorderThickness="0" 
                       Foreground="#F44336" Margin="8,0,0,0" Padding="4,2"
                       Command="{Binding ClearErrorCommand}" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>

        <!-- Success Message -->
        <Border Background="#E8F5E8" BorderBrush="#4CAF50" BorderThickness="1" 
                CornerRadius="4" Padding="12" Margin="0,0,0,8"
                Visibility="{Binding HasSuccess, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="✓" Foreground="#4CAF50" FontSize="16" VerticalAlignment="Center" Margin="0,0,8,0"/>
                <TextBlock Text="{Binding SuccessMessage}" Foreground="#2E7D32" 
                          TextWrapping="Wrap" VerticalAlignment="Center"/>
                <Button Content="✕" Background="Transparent" BorderThickness="0" 
                       Foreground="#4CAF50" Margin="8,0,0,0" Padding="4,2"
                       Command="{Binding ClearSuccessCommand}" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>

        <!-- Loading Indicator -->
        <Border Background="#E3F2FD" BorderBrush="#2196F3" BorderThickness="1" 
                CornerRadius="4" Padding="12" Margin="0,0,0,8"
                Visibility="{Binding IsLoading, Converter={StaticResource BooleanToVisibilityConverter}}">
            <StackPanel Orientation="Horizontal">
                <ProgressBar IsIndeterminate="True" Width="20" Height="20" Margin="0,0,8,0" VerticalAlignment="Center"/>
                <TextBlock Text="Loading..." Foreground="#1976D2" VerticalAlignment="Center"/>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

### **Error Dialog for Critical Errors**
```csharp
// BusBuddy.WPF/Views/Dialogs/ErrorDialog.xaml.cs
public partial class ErrorDialog : Window
{
    private static readonly ILogger Logger = Log.ForContext<ErrorDialog>();

    public ErrorDialog(string title, string message, Exception? exception = null)
    {
        InitializeComponent();
        
        Title = title;
        ErrorTitleTextBlock.Text = title;
        ErrorMessageTextBlock.Text = message;
        
        if (exception != null)
        {
            DetailsTextBox.Text = FormatException(exception);
            Logger.Error(exception, "Error dialog displayed: {Title} - {Message}", title, message);
        }
        else
        {
            DetailsExpander.Visibility = Visibility.Collapsed;
            Logger.Warning("Error dialog displayed: {Title} - {Message}", title, message);
        }
    }

    private string FormatException(Exception exception)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Exception Type: {exception.GetType().Name}");
        sb.AppendLine($"Message: {exception.Message}");
        sb.AppendLine($"Source: {exception.Source}");
        sb.AppendLine();
        sb.AppendLine("Stack Trace:");
        sb.AppendLine(exception.StackTrace);
        
        if (exception.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine("Inner Exception:");
            sb.AppendLine(FormatException(exception.InnerException));
        }
        
        return sb.ToString();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var errorInfo = $"{ErrorTitleTextBlock.Text}\n\n{ErrorMessageTextBlock.Text}";
            if (!string.IsNullOrEmpty(DetailsTextBox.Text))
            {
                errorInfo += $"\n\nDetails:\n{DetailsTextBox.Text}";
            }
            
            Clipboard.SetText(errorInfo);
            Logger.Information("Error details copied to clipboard");
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to copy error details to clipboard");
        }
    }
}
```

---

## 🧪 **Testing Error Handling**

### **Service Error Handling Tests**
```csharp
// BusBuddy.Tests/Core/Services/StudentServiceErrorTests.cs
[TestFixture]
[Category("Service")]
public class StudentServiceErrorTests
{
    private Mock<IStudentRepository> _mockRepository;
    private StudentService _studentService;
    private ILogger _logger;

    [SetUp]
    public void Setup()
    {
        _mockRepository = new Mock<IStudentRepository>();
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        
        Log.Logger = _logger;
        _studentService = new StudentService(_mockRepository.Object);
    }

    [TearDown]
    public void TearDown()
    {
        Log.CloseAndFlush();
    }

    [Test]
    public async Task CreateStudentAsync_WithNullRequest_ReturnsValidationError()
    {
        // Act
        var result = await _studentService.CreateStudentAsync(null!);

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Contains.Item("Request cannot be null"));
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public async Task CreateStudentAsync_WithInvalidData_ReturnsValidationErrors()
    {
        // Arrange
        var request = new CreateStudentRequest
        {
            FirstName = "", // Invalid - empty
            LastName = "Doe",
            Grade = 15, // Invalid - out of range
            DateOfBirth = DateTime.Today.AddDays(1) // Invalid - future date
        };

        // Act
        var result = await _studentService.CreateStudentAsync(request);

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors, Has.Length.GreaterThan(0));
        Assert.That(result.Errors, Contains.Item("First name is required"));
        Assert.That(result.Errors, Contains.Item("Grade must be between 0 and 12"));
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public async Task CreateStudentAsync_WithDuplicateStudent_ReturnsDuplicateError()
    {
        // Arrange
        var request = new CreateStudentRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Grade = 5,
            DateOfBirth = DateTime.Today.AddYears(-10),
            Address = "123 Main St"
        };

        _mockRepository.Setup(r => r.StudentExistsAsync(
            request.FirstName, request.LastName, request.DateOfBirth))
            .ReturnsAsync(true);

        // Act
        var result = await _studentService.CreateStudentAsync(request);

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("already exists"));
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Student>()), Times.Never);
    }

    [Test]
    public async Task CreateStudentAsync_WithDatabaseError_ReturnsGenericError()
    {
        // Arrange
        var request = new CreateStudentRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Grade = 5,
            DateOfBirth = DateTime.Today.AddYears(-10),
            Address = "123 Main St"
        };

        _mockRepository.Setup(r => r.StudentExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(false);

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Student>()))
            .ThrowsAsync(new DbUpdateException("Database connection failed"));

        // Act
        var result = await _studentService.CreateStudentAsync(request);

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("Database error occurred"));
    }

    [Test]
    public async Task GetStudentsByGradeAsync_WithInvalidGrade_ReturnsValidationError()
    {
        // Act
        var result = await _studentService.GetStudentsByGradeAsync(-1);

        // Assert
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Errors[0], Does.Contain("Grade must be between 0 and 12"));
        _mockRepository.Verify(r => r.GetStudentsByGradeAsync(It.IsAny<int>()), Times.Never);
    }
}
```

---

## 📋 **PowerShell Error Commands**

### **Error Log Analysis Commands**
```powershell
# bb-error-logs: View recent error logs
function Show-BusBuddyErrorLogs {
    [CmdletBinding()]
    param(
        [int]$Hours = 24,
        [string]$Level = "Warning"
    )
    
    $logPath = "logs"
    if (-not (Test-Path $logPath)) {
        Write-Warning "Log directory not found: $logPath"
        return
    }
    
    $cutoffTime = (Get-Date).AddHours(-$Hours)
    $errorFiles = Get-ChildItem "$logPath\busbuddy-errors-*.log" | 
                  Where-Object { $_.LastWriteTime -gt $cutoffTime }
    
    if (-not $errorFiles) {
        Write-Information "No error logs found in the last $Hours hours" -InformationAction Continue
        return
    }
    
    Write-Information "Error logs from the last $Hours hours:" -InformationAction Continue
    foreach ($file in $errorFiles) {
        Write-Information "=== $($file.Name) ===" -InformationAction Continue
        Get-Content $file.FullName | Select-Object -Last 20
        Write-Information "" -InformationAction Continue
    }
}

# bb-error-summary: Summarize error patterns
function Get-BusBuddyErrorSummary {
    [CmdletBinding()]
    param(
        [int]$Days = 7
    )
    
    $logPath = "logs"
    $cutoffDate = (Get-Date).AddDays(-$Days)
    
    $errorFiles = Get-ChildItem "$logPath\busbuddy-errors-*.log" | 
                  Where-Object { $_.LastWriteTime -gt $cutoffDate }
    
    if (-not $errorFiles) {
        Write-Information "No error logs found in the last $Days days" -InformationAction Continue
        return
    }
    
    $errorSummary = @{}
    
    foreach ($file in $errorFiles) {
        $content = Get-Content $file.FullName
        foreach ($line in $content) {
            if ($line -match '\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) (\w{3})\] (.+?): (.+)') {
                $timestamp = $matches[1]
                $level = $matches[2]
                $source = $matches[3]
                $message = $matches[4]
                
                $key = "$source - $level"
                if (-not $errorSummary.ContainsKey($key)) {
                    $errorSummary[$key] = @{
                        Count = 0
                        LastSeen = $timestamp
                        Sample = $message
                    }
                }
                $errorSummary[$key].Count++
                $errorSummary[$key].LastSeen = $timestamp
            }
        }
    }
    
    Write-Information "Error Summary (Last $Days days):" -InformationAction Continue
    $errorSummary.GetEnumerator() | Sort-Object { $_.Value.Count } -Descending | ForEach-Object {
        Write-Information "$($_.Key): $($_.Value.Count) occurrences (Last: $($_.Value.LastSeen))" -InformationAction Continue
        Write-Information "  Sample: $($_.Value.Sample)" -InformationAction Continue
        Write-Information "" -InformationAction Continue
    }
}

# bb-clear-logs: Clear old log files
function Clear-BusBuddyLogs {
    [CmdletBinding()]
    param(
        [int]$DaysToKeep = 30,
        [switch]$WhatIf
    )
    
    $logPath = "logs"
    $cutoffDate = (Get-Date).AddDays(-$DaysToKeep)
    
    $oldLogFiles = Get-ChildItem "$logPath\*.log" | 
                   Where-Object { $_.LastWriteTime -lt $cutoffDate }
    
    if (-not $oldLogFiles) {
        Write-Information "No log files older than $DaysToKeep days found" -InformationAction Continue
        return
    }
    
    Write-Information "Log files to remove (older than $DaysToKeep days):" -InformationAction Continue
    foreach ($file in $oldLogFiles) {
        Write-Information "  $($file.Name) - $($file.LastWriteTime)" -InformationAction Continue
        
        if (-not $WhatIf) {
            Remove-Item $file.FullName -Force
        }
    }
    
    if ($WhatIf) {
        Write-Information "Use -WhatIf:$false to actually remove files" -InformationAction Continue
    } else {
        Write-Information "Removed $($oldLogFiles.Count) old log files" -InformationAction Continue
    }
}

# Export functions
Export-ModuleMember -Function Show-BusBuddyErrorLogs, Get-BusBuddyErrorSummary, Clear-BusBuddyLogs
```

---

## 📋 **Quick Reference**

### **Serilog Log Levels**
- **Verbose**: Detailed trace information (debugging)
- **Debug**: Debug information (development)
- **Information**: General application flow (normal operation)
- **Warning**: Unexpected situations that don't stop the application
- **Error**: Errors and exceptions that affect functionality
- **Fatal**: Critical errors that may cause the application to terminate

### **Exception Handling Best Practices**
1. **Log at the appropriate level**: Use Warning for expected errors, Error for unexpected
2. **Include context**: Log relevant data with structured properties
3. **Don't log and rethrow**: Either log and handle, or rethrow without logging
4. **User-friendly messages**: Never show technical error messages to end users
5. **Async patterns**: Use proper async exception handling in ViewModels and Services

### **PowerShell Error Commands**
```powershell
# View recent errors
bb-error-logs -Hours 24

# Get error summary
bb-error-summary -Days 7

# Clear old logs
bb-clear-logs -DaysToKeep 30

# Load error handling reference
bb-copilot-ref Error-Handling
```

---

**📋 Note**: This reference provides GitHub Copilot with comprehensive error handling patterns using Serilog and structured exception management. Use `bb-copilot-ref Error-Handling` to load these patterns before implementing error handling features.
