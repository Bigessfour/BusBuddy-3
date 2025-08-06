# 🎓 Student Entry Examples - Syncfusion WPF Patterns

**Part of BusBuddy Copilot Reference Hub**  
**Last Updated**: August 3, 2025  
**Purpose**: Provide GitHub Copilot with student data entry patterns for enhanced suggestions

---

## 📋 **Student Data Entry Form Structure**

### **Core Student Model Pattern**
```csharp
// BusBuddy.Core/Models/Student.cs
public class Student
{
    public int StudentId { get; set; }
    
    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;
    
    [Range(1, 12)]
    public int Grade { get; set; }
    
    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;
    
    [Phone]
    public string? EmergencyContact { get; set; }
    
    public DateTime DateOfBirth { get; set; }
    
    // Navigation Properties
    public int? RouteId { get; set; }
    public Route? Route { get; set; }
    
    public List<StudentNote> Notes { get; set; } = new();
}

// Computed Properties
public string FullName => $"{FirstName} {LastName}";
public int Age => DateTime.Now.Year - DateOfBirth.Year;
```

---

## 🎨 **Syncfusion WPF Form Patterns**

### **Student Entry View (XAML)**
```xml
<!-- BusBuddy.WPF/Views/StudentEntryView.xaml -->
<UserControl x:Class="BusBuddy.WPF.Views.StudentEntryView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:syncfusion="http://schemas.syncfusion.com/wpf">
    
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <TextBlock Grid.Row="0" Text="Student Information" 
                   FontSize="24" FontWeight="Bold" Margin="0,0,0,20"/>
        
        <!-- First Name -->
        <StackPanel Grid.Row="1" Margin="0,0,0,10">
            <TextBlock Text="First Name *" FontWeight="SemiBold"/>
            <syncfusion:SfTextBox x:Name="FirstNameTextBox"
                                  Text="{Binding Student.FirstName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                  Watermark="Enter first name"
                                  HasError="{Binding HasFirstNameError}"
                                  ErrorText="{Binding FirstNameError}"/>
        </StackPanel>
        
        <!-- Last Name -->
        <StackPanel Grid.Row="2" Margin="0,0,0,10">
            <TextBlock Text="Last Name *" FontWeight="SemiBold"/>
            <syncfusion:SfTextBox x:Name="LastNameTextBox"
                                  Text="{Binding Student.LastName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                  Watermark="Enter last name"
                                  HasError="{Binding HasLastNameError}"
                                  ErrorText="{Binding LastNameError}"/>
        </StackPanel>
        
        <!-- Grade Selection -->
        <StackPanel Grid.Row="3" Margin="0,0,0,10">
            <TextBlock Text="Grade *" FontWeight="SemiBold"/>
            <syncfusion:SfComboBox x:Name="GradeComboBox"
                                   SelectedValue="{Binding Student.Grade, Mode=TwoWay}"
                                   ItemsSource="{Binding AvailableGrades}"
                                   DisplayMemberPath="DisplayName"
                                   SelectedValuePath="Value"
                                   Watermark="Select grade level"/>
        </StackPanel>
        
        <!-- Date of Birth -->
        <StackPanel Grid.Row="4" Margin="0,0,0,10">
            <TextBlock Text="Date of Birth *" FontWeight="SemiBold"/>
            <syncfusion:SfDatePicker x:Name="DateOfBirthPicker"
                                     SelectedDate="{Binding Student.DateOfBirth, Mode=TwoWay}"
                                     Watermark="Select date of birth"
                                     MaxDate="{Binding MaxBirthDate}"/>
        </StackPanel>
        
        <!-- Address -->
        <StackPanel Grid.Row="5" Margin="0,0,0,10">
            <TextBlock Text="Home Address" FontWeight="SemiBold"/>
            <syncfusion:SfTextBox x:Name="AddressTextBox"
                                  Text="{Binding Student.Address, Mode=TwoWay}"
                                  Watermark="Enter home address"
                                  AcceptsReturn="True"
                                  TextWrapping="Wrap"
                                  Height="60"/>
        </StackPanel>
        
        <!-- Emergency Contact -->
        <StackPanel Grid.Row="6" Margin="0,0,0,20">
            <TextBlock Text="Emergency Contact Phone" FontWeight="SemiBold"/>
            <syncfusion:SfMaskedTextBox x:Name="EmergencyContactTextBox"
                                        Value="{Binding Student.EmergencyContact, Mode=TwoWay}"
                                        MaskType="Simple"
                                        Mask="(000) 000-0000"
                                        Watermark="(555) 123-4567"/>
        </StackPanel>
        
        <!-- Action Buttons -->
        <StackPanel Grid.Row="7" Orientation="Horizontal" HorizontalAlignment="Right">
            <syncfusion:SfButton Content="Cancel"
                                 Command="{Binding CancelCommand}"
                                 Style="{StaticResource SecondaryButtonStyle}"
                                 Margin="0,0,10,0"/>
            <syncfusion:SfButton Content="Save Student"
                                 Command="{Binding SaveStudentCommand}"
                                 Style="{StaticResource PrimaryButtonStyle}"
                                 IsEnabled="{Binding CanSaveStudent}"/>
        </StackPanel>
    </Grid>
</UserControl>
```

### **Student List View with Data Grid**
```xml
<!-- Student list with search and filtering -->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
        <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>
    
    <!-- Search Bar -->
    <syncfusion:SfTextBox Grid.Row="0" 
                          Text="{Binding SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                          Watermark="Search students..."
                          Margin="0,0,0,10">
        <syncfusion:SfTextBox.LeadingView>
            <Path Data="M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z"/>
        </syncfusion:SfTextBox.LeadingView>
    </syncfusion:SfTextBox>
    
    <!-- Filter Options -->
    <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
        <TextBlock Text="Filter by Grade:" VerticalAlignment="Center" Margin="0,0,10,0"/>
        <syncfusion:SfComboBox ItemsSource="{Binding GradeFilters}"
                               SelectedValue="{Binding SelectedGradeFilter, Mode=TwoWay}"
                               DisplayMemberPath="DisplayName"
                               SelectedValuePath="Value"
                               Width="120"/>
    </StackPanel>
    
    <!-- Students Data Grid -->
    <syncfusion:SfDataGrid Grid.Row="2"
                           x:Name="StudentsDataGrid"
                           ItemsSource="{Binding FilteredStudents}"
                           SelectedItem="{Binding SelectedStudent, Mode=TwoWay}"
                           AutoGenerateColumns="False"
                           AllowEditing="False"
                           AllowSorting="True"
                           AllowFiltering="True"
                           SelectionMode="Single"
                           GridLinesVisibility="Both">
        
        <syncfusion:SfDataGrid.Columns>
            <syncfusion:GridTextColumn HeaderText="Student ID" 
                                     MappingName="StudentId" 
                                     Width="100"/>
            <syncfusion:GridTextColumn HeaderText="First Name" 
                                     MappingName="FirstName" 
                                     Width="120"/>
            <syncfusion:GridTextColumn HeaderText="Last Name" 
                                     MappingName="LastName" 
                                     Width="120"/>
            <syncfusion:GridNumericColumn HeaderText="Grade" 
                                        MappingName="Grade" 
                                        Width="80"/>
            <syncfusion:GridTextColumn HeaderText="Age" 
                                     MappingName="Age" 
                                     Width="60"/>
            <syncfusion:GridTextColumn HeaderText="Address" 
                                     MappingName="Address" 
                                     Width="200"/>
            <syncfusion:GridTextColumn HeaderText="Route" 
                                     MappingName="Route.RouteName" 
                                     Width="100"/>
        </syncfusion:SfDataGrid.Columns>
        
        <syncfusion:SfDataGrid.ContextMenuSettings>
            <syncfusion:ContextMenuSettings>
                <syncfusion:ContextMenuSettings.ContextMenuItems>
                    <syncfusion:GridContextMenuItemInfo ItemName="Edit Student" 
                                                       CommandParameter="{Binding SelectedStudent}"/>
                    <syncfusion:GridContextMenuItemInfo ItemName="Assign Route" 
                                                       CommandParameter="{Binding SelectedStudent}"/>
                    <syncfusion:GridContextMenuItemInfo ItemName="View Details" 
                                                       CommandParameter="{Binding SelectedStudent}"/>
                </syncfusion:ContextMenuSettings.ContextMenuItems>
            </syncfusion:ContextMenuSettings>
        </syncfusion:SfDataGrid.ContextMenuSettings>
    </syncfusion:SfDataGrid>
    
    <!-- Action Bar -->
    <StackPanel Grid.Row="3" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
        <syncfusion:SfButton Content="Add Student"
                             Command="{Binding AddStudentCommand}"
                             Style="{StaticResource PrimaryButtonStyle}"
                             Margin="0,0,10,0"/>
        <syncfusion:SfButton Content="Edit Student"
                             Command="{Binding EditStudentCommand}"
                             IsEnabled="{Binding HasSelectedStudent}"
                             Style="{StaticResource SecondaryButtonStyle}"
                             Margin="0,0,10,0"/>
        <syncfusion:SfButton Content="Delete Student"
                             Command="{Binding DeleteStudentCommand}"
                             IsEnabled="{Binding HasSelectedStudent}"
                             Style="{StaticResource DangerButtonStyle}"/>
    </StackPanel>
</Grid>
```

---

## 🎯 **ViewModel Patterns**

### **Student Entry ViewModel**
```csharp
// BusBuddy.WPF/ViewModels/StudentEntryViewModel.cs
public class StudentEntryViewModel : BaseViewModel
{
    private readonly IStudentService _studentService;
    private readonly ILogger<StudentEntryViewModel> _logger;
    private Student _student = new();
    private bool _isEditMode;

    public StudentEntryViewModel(IStudentService studentService, ILogger<StudentEntryViewModel> logger)
    {
        _studentService = studentService;
        _logger = logger;
        
        SaveStudentCommand = new RelayCommand(async () => await SaveStudentAsync(), CanSaveStudent);
        CancelCommand = new RelayCommand(Cancel);
        
        InitializeGrades();
    }

    public Student Student
    {
        get => _student;
        set
        {
            if (SetProperty(ref _student, value))
            {
                ValidateStudent();
                SaveStudentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetProperty(ref _isEditMode, value);
    }

    public ObservableCollection<GradeOption> AvailableGrades { get; } = new();
    
    public DateTime MaxBirthDate => DateTime.Today.AddYears(-4); // Minimum age 4
    
    // Validation Properties
    public bool HasFirstNameError => !string.IsNullOrWhiteSpace(FirstNameError);
    public string FirstNameError { get; private set; } = string.Empty;
    
    public bool HasLastNameError => !string.IsNullOrWhiteSpace(LastNameError);
    public string LastNameError { get; private set; } = string.Empty;
    
    public bool CanSaveStudent => !HasFirstNameError && !HasLastNameError && 
                                  !string.IsNullOrWhiteSpace(Student.FirstName) &&
                                  !string.IsNullOrWhiteSpace(Student.LastName) &&
                                  Student.Grade > 0;

    // Commands
    public RelayCommand SaveStudentCommand { get; }
    public RelayCommand CancelCommand { get; }

    private void InitializeGrades()
    {
        for (int i = 1; i <= 12; i++)
        {
            AvailableGrades.Add(new GradeOption 
            { 
                Value = i, 
                DisplayName = $"Grade {i}" 
            });
        }
        AvailableGrades.Add(new GradeOption 
        { 
            Value = 0, 
            DisplayName = "Pre-K" 
        });
    }

    private async Task SaveStudentAsync()
    {
        try
        {
            IsLoading = true;
            
            if (IsEditMode)
            {
                await _studentService.UpdateStudentAsync(Student);
                _logger.LogInformation("Updated student {StudentId}", Student.StudentId);
            }
            else
            {
                await _studentService.AddStudentAsync(Student);
                _logger.LogInformation("Added new student {FirstName} {LastName}", 
                    Student.FirstName, Student.LastName);
            }
            
            ShowSuccess($"Student {Student.FullName} saved successfully!");
            
            // Navigate back or reset form
            if (!IsEditMode)
            {
                Student = new Student(); // Reset for new entry
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save student");
            ShowError($"Failed to save student: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ValidateStudent()
    {
        // First Name Validation
        FirstNameError = string.IsNullOrWhiteSpace(Student.FirstName) ? 
            "First name is required" : 
            Student.FirstName.Length > 50 ? "First name must be 50 characters or less" : 
            string.Empty;
        
        // Last Name Validation
        LastNameError = string.IsNullOrWhiteSpace(Student.LastName) ? 
            "Last name is required" : 
            Student.LastName.Length > 50 ? "Last name must be 50 characters or less" : 
            string.Empty;
        
        OnPropertyChanged(nameof(HasFirstNameError));
        OnPropertyChanged(nameof(FirstNameError));
        OnPropertyChanged(nameof(HasLastNameError));
        OnPropertyChanged(nameof(LastNameError));
        OnPropertyChanged(nameof(CanSaveStudent));
    }

    private void Cancel()
    {
        // Navigate back or reset
        if (!IsEditMode)
        {
            Student = new Student();
        }
        // Implementation depends on navigation service
    }

    public void LoadStudent(int studentId)
    {
        // Load existing student for editing
        IsEditMode = true;
        // Implementation with studentService.GetStudentByIdAsync
    }
}

public class GradeOption
{
    public int Value { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
```

---

## 💾 **Data Service Patterns**

### **Student Service Implementation**
```csharp
// BusBuddy.Core/Services/StudentService.cs
public interface IStudentService
{
    Task<List<Student>> GetAllStudentsAsync();
    Task<Student?> GetStudentByIdAsync(int studentId);
    Task<List<Student>> SearchStudentsAsync(string searchTerm);
    Task<List<Student>> GetStudentsByGradeAsync(int grade);
    Task AddStudentAsync(Student student);
    Task UpdateStudentAsync(Student student);
    Task DeleteStudentAsync(int studentId);
    Task<bool> StudentExistsAsync(string firstName, string lastName, DateTime dateOfBirth);
}

public class StudentService : IStudentService
{
    private readonly BusBuddyContext _context;
    private readonly ILogger<StudentService> _logger;

    public StudentService(BusBuddyContext context, ILogger<StudentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Student>> GetAllStudentsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all students");
            
            return await _context.Students
                .Include(s => s.Route)
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students");
            throw;
        }
    }

    public async Task<Student?> GetStudentByIdAsync(int studentId)
    {
        try
        {
            return await _context.Students
                .Include(s => s.Route)
                .Include(s => s.Notes)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<List<Student>> SearchStudentsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllStudentsAsync();

        try
        {
            var lowerSearchTerm = searchTerm.ToLower();
            
            return await _context.Students
                .Include(s => s.Route)
                .Where(s => s.FirstName.ToLower().Contains(lowerSearchTerm) ||
                           s.LastName.ToLower().Contains(lowerSearchTerm) ||
                           s.Address.ToLower().Contains(lowerSearchTerm) ||
                           s.StudentId.ToString().Contains(searchTerm))
                .OrderBy(s => s.LastName)
                .ThenBy(s => s.FirstName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching students with term: {SearchTerm}", searchTerm);
            throw;
        }
    }

    public async Task AddStudentAsync(Student student)
    {
        try
        {
            // Validate uniqueness
            var exists = await StudentExistsAsync(student.FirstName, student.LastName, student.DateOfBirth);
            if (exists)
            {
                throw new InvalidOperationException("A student with the same name and birth date already exists");
            }

            _context.Students.Add(student);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Added student {StudentId}: {FirstName} {LastName}", 
                student.StudentId, student.FirstName, student.LastName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding student {FirstName} {LastName}", 
                student.FirstName, student.LastName);
            throw;
        }
    }

    public async Task UpdateStudentAsync(Student student)
    {
        try
        {
            _context.Students.Update(student);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Updated student {StudentId}", student.StudentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating student {StudentId}", student.StudentId);
            throw;
        }
    }

    public async Task DeleteStudentAsync(int studentId)
    {
        try
        {
            var student = await GetStudentByIdAsync(studentId);
            if (student != null)
            {
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Deleted student {StudentId}", studentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting student {StudentId}", studentId);
            throw;
        }
    }

    public async Task<bool> StudentExistsAsync(string firstName, string lastName, DateTime dateOfBirth)
    {
        return await _context.Students
            .AnyAsync(s => s.FirstName.ToLower() == firstName.ToLower() &&
                          s.LastName.ToLower() == lastName.ToLower() &&
                          s.DateOfBirth.Date == dateOfBirth.Date);
    }
}
```

---

## 🎨 **Style Resources**

### **Button Styles for Student Forms**
```xml
<!-- Add to App.xaml or Resource Dictionary -->
<Style x:Key="PrimaryButtonStyle" TargetType="syncfusion:SfButton">
    <Setter Property="Background" Value="#007ACC"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="15,8"/>
    <Setter Property="MinWidth" Value="100"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="CornerRadius" Value="4"/>
</Style>

<Style x:Key="SecondaryButtonStyle" TargetType="syncfusion:SfButton">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="#007ACC"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="15,8"/>
    <Setter Property="MinWidth" Value="100"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="BorderBrush" Value="#007ACC"/>
    <Setter Property="CornerRadius" Value="4"/>
</Style>

<Style x:Key="DangerButtonStyle" TargetType="syncfusion:SfButton">
    <Setter Property="Background" Value="#DC3545"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Padding" Value="15,8"/>
    <Setter Property="MinWidth" Value="100"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="CornerRadius" Value="4"/>
</Style>
```

---

## 🧪 **Testing Patterns**

### **Student Service Unit Tests**
```csharp
// BusBuddy.Tests/Core/StudentServiceTests.cs
[TestFixture]
[Category("Unit")]
public class StudentServiceTests
{
    private StudentService _studentService;
    private BusBuddyContext _context;
    private ILogger<StudentService> _logger;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<BusBuddyContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BusBuddyContext(options);
        _logger = Substitute.For<ILogger<StudentService>>();
        _studentService = new StudentService(_context, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task AddStudentAsync_ValidStudent_AddsSuccessfully()
    {
        // Arrange
        var student = new Student
        {
            FirstName = "John",
            LastName = "Doe",
            Grade = 5,
            DateOfBirth = DateTime.Today.AddYears(-10),
            Address = "123 Main St"
        };

        // Act
        await _studentService.AddStudentAsync(student);

        // Assert
        var addedStudent = await _context.Students.FirstOrDefaultAsync();
        Assert.That(addedStudent, Is.Not.Null);
        Assert.That(addedStudent.FirstName, Is.EqualTo("John"));
        Assert.That(addedStudent.LastName, Is.EqualTo("Doe"));
    }

    [Test]
    public async Task AddStudentAsync_DuplicateStudent_ThrowsException()
    {
        // Arrange
        var existingStudent = new Student
        {
            FirstName = "Jane",
            LastName = "Smith",
            Grade = 3,
            DateOfBirth = DateTime.Today.AddYears(-8)
        };
        
        await _context.Students.AddAsync(existingStudent);
        await _context.SaveChangesAsync();

        var duplicateStudent = new Student
        {
            FirstName = "Jane",
            LastName = "Smith",
            DateOfBirth = DateTime.Today.AddYears(-8),
            Grade = 4 // Different grade, same name and DOB
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _studentService.AddStudentAsync(duplicateStudent));
        
        Assert.That(ex.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task SearchStudentsAsync_ValidSearchTerm_ReturnsMatchingStudents()
    {
        // Arrange
        await SeedTestData();

        // Act
        var results = await _studentService.SearchStudentsAsync("John");

        // Assert
        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0].FirstName, Is.EqualTo("John"));
    }

    private async Task SeedTestData()
    {
        var students = new[]
        {
            new Student { FirstName = "John", LastName = "Doe", Grade = 5, DateOfBirth = DateTime.Today.AddYears(-10) },
            new Student { FirstName = "Jane", LastName = "Smith", Grade = 3, DateOfBirth = DateTime.Today.AddYears(-8) },
            new Student { FirstName = "Bob", LastName = "Johnson", Grade = 7, DateOfBirth = DateTime.Today.AddYears(-12) }
        };

        await _context.Students.AddRangeAsync(students);
        await _context.SaveChangesAsync();
    }
}
```

---

## 📚 **Quick Reference Commands**

### **Create New Student Entry Feature**
```powershell
# Generate student entry scaffolding
bb-copilot-ref Student-Entry-Examples  # Load this reference
bb-scaffold Student Entry             # Generate basic structure (if implemented)

# Run focused tests
bb-test -TestSuite Core -Filter "Student"
bb-test-watch -TestSuite Unit         # Continuous testing
```

### **Common Syncfusion Student Form Controls**
- **SfTextBox**: Basic text input with validation
- **SfComboBox**: Grade selection dropdown
- **SfDatePicker**: Date of birth selection
- **SfMaskedTextBox**: Phone number formatting
- **SfDataGrid**: Student list display
- **SfButton**: Form actions (Save, Cancel, Delete)

### **Entity Framework Patterns**
- **Include()**: Load related data (Routes, Notes)
- **Where()**: Filter students by criteria
- **OrderBy()**: Sort student lists
- **AnyAsync()**: Check for duplicates
- **FirstOrDefaultAsync()**: Get single student

---

**📋 Note**: This reference provides GitHub Copilot with comprehensive patterns for student data entry in BusBuddy. Use `bb-copilot-ref Student-Entry-Examples` to load these patterns before implementing student features.
