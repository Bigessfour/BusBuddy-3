# 🗺️ Route Assignment Logic - xAI Grok Integration Patterns

**Part of BusBuddy Copilot Reference Hub**  
**Last Updated**: August 3, 2025  
**Purpose**: Provide GitHub Copilot with route assignment and optimization patterns using xAI Grok API

---

## 🧠 **Route Assignment Architecture**

### **Core Route Models**

```csharp
// BusBuddy.Core/Models/Route.cs
public class Route
{
    public int RouteId { get; set; }

    [Required, MaxLength(50)]
    public string RouteName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public double EstimatedDistance { get; set; } // in miles
    public int EstimatedDuration { get; set; } // in minutes

    // Navigation Properties
    public int BusId { get; set; }
    public Bus Bus { get; set; } = null!;

    public int DriverId { get; set; }
    public Driver Driver { get; set; } = null!;

    public List<Student> Students { get; set; } = new();
    public List<RouteStop> Stops { get; set; } = new();

    // Computed Properties
    public int StudentCount => Students.Count;
    public bool IsAtCapacity => Students.Count >= Bus.Capacity;
    public double UtilizationRate => Bus.Capacity > 0 ? (double)Students.Count / Bus.Capacity : 0;
}

public class Bus
{
    public int BusId { get; set; }

    [Required, MaxLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Make { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }
    public int Capacity { get; set; }
    public int Mileage { get; set; }

    public BusStatus Status { get; set; } = BusStatus.Active;

    // Navigation Properties
    public List<Route> Routes { get; set; } = new();
}

public class Driver
{
    public int DriverId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string LicenseNumber { get; set; } = string.Empty;

    public DateTime LicenseExpiry { get; set; }

    [Phone]
    public string Phone { get; set; } = string.Empty;

    public DriverStatus Status { get; set; } = DriverStatus.Active;

    // Navigation Properties
    public List<Route> Routes { get; set; } = new();
}

public class RouteStop
{
    public int RouteStopId { get; set; }
    public int RouteId { get; set; }
    public Route Route { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int StopOrder { get; set; }
    public TimeSpan ScheduledTime { get; set; }

    public List<Student> Students { get; set; } = new();
}

public enum BusStatus { Active, Maintenance, OutOfService }
public enum DriverStatus { Active, OnLeave, Inactive }
```

---

## 🤖 **xAI Grok API Integration**

### **Grok API Service Implementation**

```csharp
// BusBuddy.Core/Services/GrokRouteOptimizationService.cs
public interface IGrokRouteOptimizationService
{
    Task<RouteOptimizationResult> OptimizeRoutesAsync(List<Student> students, List<Bus> buses, List<Driver> drivers);
    Task<StudentAssignmentResult> AssignStudentToOptimalRouteAsync(Student student, List<Route> availableRoutes);
    Task<RouteEfficiencyAnalysis> AnalyzeRouteEfficiencyAsync(Route route);
    Task<string> GenerateRouteRecommendationsAsync(RouteOptimizationRequest request);
}

public class GrokRouteOptimizationService : IGrokRouteOptimizationService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GrokRouteOptimizationService> _logger;
    private readonly string _apiKey;
    private readonly string _baseUrl;

    public GrokRouteOptimizationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GrokRouteOptimizationService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = _configuration["XAI:ApiKey"] ?? throw new InvalidOperationException("XAI API key not configured");
        _baseUrl = _configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1";

        _httpClient.DefaultRequestHeaders.Authorization = new("Bearer", _apiKey);
    }

    public async Task<RouteOptimizationResult> OptimizeRoutesAsync(
        List<Student> students,
        List<Bus> buses,
        List<Driver> drivers)
    {
        try
        {
            _logger.LogInformation("Starting route optimization for {StudentCount} students", students.Count);

            var optimizationPrompt = BuildOptimizationPrompt(students, buses, drivers);

            var grokRequest = new GrokChatRequest
            {
                Model = "grok-beta",
                Messages = new[]
                {
                    new GrokMessage
                    {
                        Role = "system",
                        Content = "You are an expert transportation logistics optimizer specializing in school bus routing. Analyze student addresses, bus capacities, and driver availability to create optimal route assignments that minimize travel time while maximizing bus utilization."
                    },
                    new GrokMessage
                    {
                        Role = "user",
                        Content = optimizationPrompt
                    }
                },
                MaxTokens = 4000,
                Temperature = 0.3 // Lower temperature for more consistent optimization
            };

            var response = await SendGrokRequestAsync(grokRequest);
            return ParseOptimizationResponse(response, students, buses, drivers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during route optimization");
            throw new RouteOptimizationException("Failed to optimize routes using Grok API", ex);
        }
    }

    public async Task<StudentAssignmentResult> AssignStudentToOptimalRouteAsync(
        Student student,
        List<Route> availableRoutes)
    {
        try
        {
            var assignmentPrompt = BuildStudentAssignmentPrompt(student, availableRoutes);

            var grokRequest = new GrokChatRequest
            {
                Model = "grok-beta",
                Messages = new[]
                {
                    new GrokMessage
                    {
                        Role = "system",
                        Content = "You are a school transportation coordinator. Analyze a student's location and available bus routes to recommend the optimal route assignment based on proximity, capacity, and efficiency."
                    },
                    new GrokMessage
                    {
                        Role = "user",
                        Content = assignmentPrompt
                    }
                },
                MaxTokens = 1000,
                Temperature = 0.2
            };

            var response = await SendGrokRequestAsync(grokRequest);
            return ParseAssignmentResponse(response, student, availableRoutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning student {StudentId} to route", student.StudentId);
            throw new StudentAssignmentException($"Failed to assign student {student.StudentId}", ex);
        }
    }

    private string BuildOptimizationPrompt(List<Student> students, List<Bus> buses, List<Driver> drivers)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Please optimize bus routes for the following school transportation scenario:");
        prompt.AppendLine();

        prompt.AppendLine("STUDENTS TO ASSIGN:");
        foreach (var student in students.Take(50)) // Limit for token constraints
        {
            prompt.AppendLine($"- Student {student.StudentId}: {student.FullName}, Grade {student.Grade}, Address: {student.Address}");
        }

        prompt.AppendLine();
        prompt.AppendLine("AVAILABLE BUSES:");
        foreach (var bus in buses)
        {
            prompt.AppendLine($"- Bus {bus.BusId}: {bus.LicensePlate}, Capacity: {bus.Capacity}, Status: {bus.Status}");
        }

        prompt.AppendLine();
        prompt.AppendLine("AVAILABLE DRIVERS:");
        foreach (var driver in drivers)
        {
            prompt.AppendLine($"- Driver {driver.DriverId}: {driver.FullName}, Status: {driver.Status}");
        }

        prompt.AppendLine();
        prompt.AppendLine("OPTIMIZATION CRITERIA:");
        prompt.AppendLine("1. Minimize total travel distance");
        prompt.AppendLine("2. Maximize bus capacity utilization (target 80-90%)");
        prompt.AppendLine("3. Group students by geographic proximity");
        prompt.AppendLine("4. Consider grade levels for appropriate grouping");
        prompt.AppendLine("5. Ensure no bus exceeds capacity");
        prompt.AppendLine();
        prompt.AppendLine("Please provide a JSON response with route assignments in the following format:");
        prompt.AppendLine(GetOptimizationResponseFormat());

        return prompt.ToString();
    }

    private string GetOptimizationResponseFormat()
    {
        return @"{
  ""routes"": [
    {
      ""routeId"": 1,
      ""routeName"": ""Route A"",
      ""busId"": 1,
      ""driverId"": 1,
      ""estimatedDistance"": 15.5,
      ""estimatedDuration"": 45,
      ""students"": [1, 2, 3],
      ""stops"": [
        {
          ""address"": ""123 Main St"",
          ""stopOrder"": 1,
          ""studentIds"": [1]
        }
      ],
      ""utilizationRate"": 0.75,
      ""efficiency"": 0.85
    }
  ],
  ""optimization_summary"": {
    ""totalStudents"": 50,
    ""totalRoutes"": 5,
    ""averageUtilization"": 0.82,
    ""totalDistance"": 125.5,
    ""recommendations"": [""Consider adding Route F for better coverage""]
  }
}";
    }

    private async Task<string> SendGrokRequestAsync(GrokChatRequest request)
    {
        var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Grok API request failed: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var grokResponse = JsonSerializer.Deserialize<GrokChatResponse>(responseContent);

        return grokResponse?.Choices?.FirstOrDefault()?.Message?.Content ??
               throw new InvalidOperationException("Empty response from Grok API");
    }

    private RouteOptimizationResult ParseOptimizationResponse(
        string grokResponse,
        List<Student> students,
        List<Bus> buses,
        List<Driver> drivers)
    {
        try
        {
            // Extract JSON from response if wrapped in text
            var jsonStart = grokResponse.IndexOf('{');
            var jsonEnd = grokResponse.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonResponse = grokResponse.Substring(jsonStart, jsonEnd - jsonStart);
                var optimizationData = JsonSerializer.Deserialize<GrokOptimizationResponse>(jsonResponse);

                return ConvertToRouteOptimizationResult(optimizationData, students, buses, drivers);
            }

            throw new InvalidOperationException("No valid JSON found in Grok response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Grok optimization response");
            throw new RouteOptimizationException("Failed to parse optimization response", ex);
        }
    }
}

// Supporting Models
public class GrokChatRequest
{
    public string Model { get; set; } = "grok-beta";
    public GrokMessage[] Messages { get; set; } = Array.Empty<GrokMessage>();
    public int MaxTokens { get; set; } = 4000;
    public double Temperature { get; set; } = 0.3;
}

public class GrokMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class GrokChatResponse
{
    public GrokChoice[]? Choices { get; set; }
}

public class GrokChoice
{
    public GrokMessage? Message { get; set; }
}

public class RouteOptimizationResult
{
    public List<OptimizedRoute> Routes { get; set; } = new();
    public OptimizationSummary Summary { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public DateTime OptimizedAt { get; set; } = DateTime.UtcNow;
}

public class OptimizedRoute
{
    public int RouteId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public int BusId { get; set; }
    public int DriverId { get; set; }
    public List<int> StudentIds { get; set; } = new();
    public List<RouteStop> Stops { get; set; } = new();
    public double EstimatedDistance { get; set; }
    public int EstimatedDuration { get; set; }
    public double UtilizationRate { get; set; }
    public double EfficiencyScore { get; set; }
}

public class StudentAssignmentResult
{
    public int RecommendedRouteId { get; set; }
    public string RecommendedRouteName { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<AlternativeRoute> Alternatives { get; set; } = new();
}

public class AlternativeRoute
{
    public int RouteId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}
```

---

## 🎯 **Route Assignment UI Patterns**

### **Route Assignment View (XAML)**

```xml
<!-- BusBuddy.WPF/Views/RouteAssignmentView.xaml -->
<UserControl x:Class="BusBuddy.WPF.Views.RouteAssignmentView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:syncfusion="http://schemas.syncfusion.com/wpf">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Header and Actions -->
        <Grid Grid.Row="0" Margin="0,0,0,20">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <TextBlock Grid.Column="0" Text="Route Assignment Management"
                       FontSize="24" FontWeight="Bold" VerticalAlignment="Center"/>

            <StackPanel Grid.Column="1" Orientation="Horizontal">
                <syncfusion:SfButton Content="🤖 Optimize All Routes"
                                     Command="{Binding OptimizeAllRoutesCommand}"
                                     Style="{StaticResource PrimaryButtonStyle}"
                                     Margin="0,0,10,0"
                                     ToolTip="Use xAI Grok to optimize all route assignments"/>
                <syncfusion:SfButton Content="➕ Create Route"
                                     Command="{Binding CreateRouteCommand}"
                                     Style="{StaticResource SecondaryButtonStyle}"/>
            </StackPanel>
        </Grid>

        <!-- Filter and Search -->
        <Grid Grid.Row="1" Margin="0,0,0,15">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="300"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <!-- Search -->
            <syncfusion:SfTextBox Grid.Column="0"
                                  Text="{Binding SearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                  Watermark="Search routes or students..."
                                  Margin="0,0,10,0"/>

            <!-- View Toggle -->
            <StackPanel Grid.Column="2" Orientation="Horizontal">
                <TextBlock Text="View:" VerticalAlignment="Center" Margin="0,0,10,0"/>
                <syncfusion:SfComboBox ItemsSource="{Binding ViewModes}"
                                       SelectedValue="{Binding SelectedViewMode, Mode=TwoWay}"
                                       DisplayMemberPath="DisplayName"
                                       SelectedValuePath="Value"
                                       Width="120"/>
            </StackPanel>
        </Grid>

        <!-- Main Content Area -->
        <Grid Grid.Row="2">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="2*"/>
                <ColumnDefinition Width="5"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Routes Grid -->
            <syncfusion:SfDataGrid Grid.Column="0"
                                   x:Name="RoutesDataGrid"
                                   ItemsSource="{Binding Routes}"
                                   SelectedItem="{Binding SelectedRoute, Mode=TwoWay}"
                                   AutoGenerateColumns="False"
                                   AllowEditing="False"
                                   AllowSorting="True"
                                   AllowFiltering="True"
                                   SelectionMode="Single">

                <syncfusion:SfDataGrid.Columns>
                    <syncfusion:GridTextColumn HeaderText="Route" MappingName="RouteName" Width="100"/>
                    <syncfusion:GridTextColumn HeaderText="Bus" MappingName="Bus.LicensePlate" Width="80"/>
                    <syncfusion:GridTextColumn HeaderText="Driver" MappingName="Driver.FullName" Width="120"/>
                    <syncfusion:GridNumericColumn HeaderText="Students" MappingName="StudentCount" Width="70"/>
                    <syncfusion:GridNumericColumn HeaderText="Capacity" MappingName="Bus.Capacity" Width="70"/>
                    <syncfusion:GridPercentColumn HeaderText="Utilization" MappingName="UtilizationRate" Width="80"/>
                    <syncfusion:GridTimeSpanColumn HeaderText="Start Time" MappingName="StartTime" Width="80"/>
                    <syncfusion:GridNumericColumn HeaderText="Distance" MappingName="EstimatedDistance" Width="80"/>
                </syncfusion:SfDataGrid.Columns>

                <syncfusion:SfDataGrid.DetailsViewDefinition>
                    <syncfusion:GridViewDefinition RelationalColumn="Students">
                        <syncfusion:GridViewDefinition.DataGrid>
                            <syncfusion:SfDataGrid AutoGenerateColumns="False">
                                <syncfusion:SfDataGrid.Columns>
                                    <syncfusion:GridTextColumn HeaderText="Student" MappingName="FullName" Width="150"/>
                                    <syncfusion:GridNumericColumn HeaderText="Grade" MappingName="Grade" Width="60"/>
                                    <syncfusion:GridTextColumn HeaderText="Address" MappingName="Address" Width="200"/>
                                    <syncfusion:GridTemplateColumn HeaderText="Actions" Width="100">
                                        <syncfusion:GridTemplateColumn.CellTemplate>
                                            <DataTemplate>
                                                <syncfusion:SfButton Content="Reassign"
                                                                     Command="{Binding DataContext.ReassignStudentCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                                     CommandParameter="{Binding}"
                                                                     Style="{StaticResource SmallButtonStyle}"/>
                                            </DataTemplate>
                                        </syncfusion:GridTemplateColumn.CellTemplate>
                                    </syncfusion:GridTemplateColumn>
                                </syncfusion:SfDataGrid.Columns>
                            </syncfusion:SfDataGrid>
                        </syncfusion:GridViewDefinition.DataGrid>
                    </syncfusion:GridViewDefinition>
                </syncfusion:SfDataGrid.DetailsViewDefinition>
            </syncfusion:SfDataGrid>

            <!-- Splitter -->
            <GridSplitter Grid.Column="1" HorizontalAlignment="Stretch" Background="LightGray"/>

            <!-- Route Details Panel -->
            <ScrollViewer Grid.Column="2" VerticalScrollBarVisibility="Auto">
                <StackPanel Margin="15,0,0,0" DataContext="{Binding SelectedRoute}">
                    <TextBlock Text="Route Details" FontSize="18" FontWeight="Bold" Margin="0,0,0,15"/>

                    <!-- Route Info -->
                    <Grid Visibility="{Binding Converter={StaticResource NullToVisibilityConverter}}">
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="Auto"/>
                        </Grid.RowDefinitions>

                        <TextBlock Grid.Row="0" Text="{Binding RouteName}" FontSize="16" FontWeight="SemiBold" Margin="0,0,0,10"/>

                        <StackPanel Grid.Row="1" Margin="0,0,0,10">
                            <TextBlock Text="Bus Information" FontWeight="SemiBold" Margin="0,0,0,5"/>
                            <TextBlock Text="{Binding Bus.LicensePlate, StringFormat='License: {0}'}"/>
                            <TextBlock Text="{Binding Bus.Make, StringFormat='Make: {0}'}"/>
                            <TextBlock Text="{Binding Bus.Capacity, StringFormat='Capacity: {0} students'}"/>
                        </StackPanel>

                        <StackPanel Grid.Row="2" Margin="0,0,0,10">
                            <TextBlock Text="Driver Information" FontWeight="SemiBold" Margin="0,0,0,5"/>
                            <TextBlock Text="{Binding Driver.FullName}"/>
                            <TextBlock Text="{Binding Driver.Phone, StringFormat='Phone: {0}'}"/>
                        </StackPanel>

                        <StackPanel Grid.Row="3" Margin="0,0,0,10">
                            <TextBlock Text="Route Statistics" FontWeight="SemiBold" Margin="0,0,0,5"/>
                            <TextBlock Text="{Binding StudentCount, StringFormat='Students: {0}'}"/>
                            <TextBlock Text="{Binding UtilizationRate, StringFormat='Utilization: {0:P0}'}"/>
                            <TextBlock Text="{Binding EstimatedDistance, StringFormat='Distance: {0:F1} miles'}"/>
                            <TextBlock Text="{Binding EstimatedDuration, StringFormat='Duration: {0} minutes'}"/>
                        </StackPanel>

                        <!-- Grok AI Actions -->
                        <StackPanel Grid.Row="4" Margin="0,0,0,15">
                            <TextBlock Text="AI Optimization" FontWeight="SemiBold" Margin="0,0,0,5"/>
                            <syncfusion:SfButton Content="🤖 Optimize This Route"
                                                 Command="{Binding DataContext.OptimizeRouteCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                 CommandParameter="{Binding}"
                                                 Style="{StaticResource SmallPrimaryButtonStyle}"
                                                 Margin="0,0,0,5"/>
                            <syncfusion:SfButton Content="📊 Analyze Efficiency"
                                                 Command="{Binding DataContext.AnalyzeEfficiencyCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                 CommandParameter="{Binding}"
                                                 Style="{StaticResource SmallSecondaryButtonStyle}"/>
                        </StackPanel>

                        <!-- Unassigned Students -->
                        <StackPanel Grid.Row="5">
                            <TextBlock Text="Quick Assign Students" FontWeight="SemiBold" Margin="0,0,0,5"/>
                            <syncfusion:SfComboBox ItemsSource="{Binding DataContext.UnassignedStudents, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                   SelectedValue="{Binding DataContext.SelectedUnassignedStudent, RelativeSource={RelativeSource AncestorType=UserControl}, Mode=TwoWay}"
                                                   DisplayMemberPath="FullName"
                                                   Watermark="Select student to assign..."
                                                   Margin="0,0,0,5"/>
                            <syncfusion:SfButton Content="🎯 AI Suggest Assignment"
                                                 Command="{Binding DataContext.SuggestAssignmentCommand, RelativeSource={RelativeSource AncestorType=UserControl}}"
                                                 Style="{StaticResource SmallPrimaryButtonStyle}"/>
                        </StackPanel>
                    </Grid>
                </StackPanel>
            </ScrollViewer>
        </Grid>

        <!-- Status Bar -->
        <Border Grid.Row="3" BorderThickness="0,1,0,0" BorderBrush="LightGray" Padding="0,10,0,0" Margin="0,15,0,0">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0" Orientation="Horizontal">
                    <TextBlock Text="{Binding Routes.Count, StringFormat='Routes: {0}'}" Margin="0,0,20,0"/>
                    <TextBlock Text="{Binding TotalAssignedStudents, StringFormat='Assigned Students: {0}'}" Margin="0,0,20,0"/>
                    <TextBlock Text="{Binding UnassignedStudents.Count, StringFormat='Unassigned: {0}'}" Margin="0,0,20,0"/>
                    <TextBlock Text="{Binding AverageUtilization, StringFormat='Avg Utilization: {0:P0}'}"/>
                </StackPanel>

                <StackPanel Grid.Column="1" Orientation="Horizontal" Visibility="{Binding IsOptimizing, Converter={StaticResource BooleanToVisibilityConverter}}">
                    <syncfusion:SfBusyIndicator IsBusy="True" Width="16" Height="16" Margin="0,0,5,0"/>
                    <TextBlock Text="AI optimization in progress..." FontStyle="Italic"/>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</UserControl>
```

---

## 🎯 **Route Assignment ViewModel**

```csharp
// BusBuddy.WPF/ViewModels/RouteAssignmentViewModel.cs
public class RouteAssignmentViewModel : BaseViewModel
{
    private readonly IGrokRouteOptimizationService _grokService;
    private readonly IRouteService _routeService;
    private readonly IStudentService _studentService;
    private readonly ILogger<RouteAssignmentViewModel> _logger;

    private ObservableCollection<Route> _routes = new();
    private ObservableCollection<Student> _unassignedStudents = new();
    private Route? _selectedRoute;
    private Student? _selectedUnassignedStudent;
    private string _searchText = string.Empty;
    private bool _isOptimizing;

    public RouteAssignmentViewModel(
        IGrokRouteOptimizationService grokService,
        IRouteService routeService,
        IStudentService studentService,
        ILogger<RouteAssignmentViewModel> logger)
    {
        _grokService = grokService;
        _routeService = routeService;
        _studentService = studentService;
        _logger = logger;

        // Commands
        OptimizeAllRoutesCommand = new RelayCommand(async () => await OptimizeAllRoutesAsync(), () => !IsOptimizing);
        OptimizeRouteCommand = new RelayCommand<Route>(async (route) => await OptimizeRouteAsync(route), (route) => route != null && !IsOptimizing);
        SuggestAssignmentCommand = new RelayCommand(async () => await SuggestAssignmentAsync(), CanSuggestAssignment);
        ReassignStudentCommand = new RelayCommand<Student>(async (student) => await ReassignStudentAsync(student));
        AnalyzeEfficiencyCommand = new RelayCommand<Route>(async (route) => await AnalyzeEfficiencyAsync(route));
        CreateRouteCommand = new RelayCommand(CreateRoute);

        LoadDataAsync();
    }

    // Properties
    public ObservableCollection<Route> Routes
    {
        get => _routes;
        set => SetProperty(ref _routes, value);
    }

    public ObservableCollection<Student> UnassignedStudents
    {
        get => _unassignedStudents;
        set => SetProperty(ref _unassignedStudents, value);
    }

    public Route? SelectedRoute
    {
        get => _selectedRoute;
        set
        {
            if (SetProperty(ref _selectedRoute, value))
            {
                OnPropertyChanged(nameof(HasSelectedRoute));
            }
        }
    }

    public Student? SelectedUnassignedStudent
    {
        get => _selectedUnassignedStudent;
        set
        {
            if (SetProperty(ref _selectedUnassignedStudent, value))
            {
                SuggestAssignmentCommand.NotifyCanExecuteChanged();
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
                FilterRoutes();
            }
        }
    }

    public bool IsOptimizing
    {
        get => _isOptimizing;
        set
        {
            if (SetProperty(ref _isOptimizing, value))
            {
                OptimizeAllRoutesCommand.NotifyCanExecuteChanged();
                OptimizeRouteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedRoute => SelectedRoute != null;

    public int TotalAssignedStudents => Routes.Sum(r => r.StudentCount);

    public double AverageUtilization => Routes.Count > 0 ? Routes.Average(r => r.UtilizationRate) : 0;

    // Commands
    public RelayCommand OptimizeAllRoutesCommand { get; }
    public RelayCommand<Route> OptimizeRouteCommand { get; }
    public RelayCommand SuggestAssignmentCommand { get; }
    public RelayCommand<Student> ReassignStudentCommand { get; }
    public RelayCommand<Route> AnalyzeEfficiencyCommand { get; }
    public RelayCommand CreateRouteCommand { get; }

    private async Task OptimizeAllRoutesAsync()
    {
        try
        {
            IsOptimizing = true;
            _logger.LogInformation("Starting global route optimization");

            ShowInfo("🤖 Grok AI is analyzing all routes for optimal assignments...");

            var allStudents = await _studentService.GetAllStudentsAsync();
            var buses = await _routeService.GetAvailableBusesAsync();
            var drivers = await _routeService.GetAvailableDriversAsync();

            var optimizationResult = await _grokService.OptimizeRoutesAsync(allStudents, buses, drivers);

            await ApplyOptimizationResult(optimizationResult);

            ShowSuccess($"✅ Route optimization complete! Optimized {optimizationResult.Routes.Count} routes with {optimizationResult.Summary.AverageUtilization:P0} average utilization.");

            if (optimizationResult.Recommendations.Any())
            {
                var recommendations = string.Join("\n", optimizationResult.Recommendations);
                ShowInfo($"🎯 AI Recommendations:\n{recommendations}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize routes");
            ShowError($"Route optimization failed: {ex.Message}");
        }
        finally
        {
            IsOptimizing = false;
        }
    }

    private async Task OptimizeRouteAsync(Route route)
    {
        try
        {
            _logger.LogInformation("Optimizing route {RouteId}", route.RouteId);

            ShowInfo($"🤖 Analyzing route {route.RouteName} for optimization...");

            var analysis = await _grokService.AnalyzeRouteEfficiencyAsync(route);

            // Show optimization suggestions
            var message = $"Route Analysis for {route.RouteName}:\n\n" +
                         $"Efficiency Score: {analysis.EfficiencyScore:P0}\n" +
                         $"Utilization: {route.UtilizationRate:P0}\n" +
                         $"Estimated Savings: {analysis.PotentialSavings}\n\n" +
                         $"Recommendations:\n{string.Join("\n", analysis.Recommendations)}";

            ShowInfo(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize route {RouteId}", route.RouteId);
            ShowError($"Route optimization failed: {ex.Message}");
        }
    }

    private async Task SuggestAssignmentAsync()
    {
        if (SelectedUnassignedStudent == null) return;

        try
        {
            _logger.LogInformation("Getting AI assignment suggestion for student {StudentId}", SelectedUnassignedStudent.StudentId);

            ShowInfo($"🤖 Grok AI is analyzing optimal route for {SelectedUnassignedStudent.FullName}...");

            var availableRoutes = Routes.Where(r => !r.IsAtCapacity).ToList();
            var suggestion = await _grokService.AssignStudentToOptimalRouteAsync(SelectedUnassignedStudent, availableRoutes);

            var recommendedRoute = Routes.FirstOrDefault(r => r.RouteId == suggestion.RecommendedRouteId);
            if (recommendedRoute != null)
            {
                var message = $"🎯 AI Recommendation for {SelectedUnassignedStudent.FullName}:\n\n" +
                             $"Recommended Route: {suggestion.RecommendedRouteName}\n" +
                             $"Confidence: {suggestion.ConfidenceScore:P0}\n\n" +
                             $"Reasoning: {suggestion.Reasoning}\n\n" +
                             "Would you like to apply this assignment?";

                var result = MessageBox.Show(message, "AI Route Assignment Suggestion",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await AssignStudentToRoute(SelectedUnassignedStudent, recommendedRoute);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assignment suggestion");
            ShowError($"Assignment suggestion failed: {ex.Message}");
        }
    }

    private bool CanSuggestAssignment()
    {
        return SelectedUnassignedStudent != null &&
               Routes.Any(r => !r.IsAtCapacity) &&
               !IsOptimizing;
    }

    private async Task AssignStudentToRoute(Student student, Route route)
    {
        try
        {
            await _routeService.AssignStudentToRouteAsync(student.StudentId, route.RouteId);

            // Update UI
            route.Students.Add(student);
            UnassignedStudents.Remove(student);

            // Refresh computed properties
            OnPropertyChanged(nameof(TotalAssignedStudents));
            OnPropertyChanged(nameof(AverageUtilization));

            ShowSuccess($"✅ {student.FullName} assigned to {route.RouteName}");

            _logger.LogInformation("Assigned student {StudentId} to route {RouteId}", student.StudentId, route.RouteId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign student to route");
            ShowError($"Assignment failed: {ex.Message}");
        }
    }
}
```

---

## 📚 **PowerShell Integration Commands**

### **Route Optimization Commands**

```powershell
# BusBuddy PowerShell Commands for Route Management

function Invoke-BusBuddyRouteOptimization {
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateSet("All", "Single", "Unassigned")]
        [string]$OptimizationType = "All",

        [Parameter()]
        [int]$RouteId,

        [Parameter()]
        [switch]$DryRun
    )

    Write-Information "🤖 Starting Grok AI route optimization..." -InformationAction Continue

    try {
        $arguments = @("--optimize-routes", "--type", $OptimizationType)

        if ($RouteId) {
            $arguments += @("--route-id", $RouteId)
        }

        if ($DryRun) {
            $arguments += "--dry-run"
        }

        $result = & dotnet run --project "BusBuddy.WPF/BusBuddy.WPF.csproj" -- $arguments

        Write-Information "✅ Route optimization completed successfully" -InformationAction Continue
        return $result
    }
    catch {
        Write-Error "❌ Route optimization failed: $_"
        throw
    }
}

function Get-BusBuddyRouteAnalysis {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$RouteId
    )

    Write-Information "📊 Analyzing route $RouteId with Grok AI..." -InformationAction Continue

    $arguments = @("--analyze-route", "--route-id", $RouteId, "--output", "json")
    $result = & dotnet run --project "BusBuddy.WPF/BusBuddy.WPF.csproj" -- $arguments

    return $result | ConvertFrom-Json
}

function Start-BusBuddyStudentAssignment {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int]$StudentId,

        [Parameter()]
        [int]$PreferredRouteId
    )

    Write-Information "🎯 Getting AI assignment suggestion for student $StudentId..." -InformationAction Continue

    $arguments = @("--suggest-assignment", "--student-id", $StudentId)

    if ($PreferredRouteId) {
        $arguments += @("--preferred-route", $PreferredRouteId)
    }

    $result = & dotnet run --project "BusBuddy.WPF/BusBuddy.WPF.csproj" -- $arguments
    return $result | ConvertFrom-Json
}

# Aliases for convenience
Set-Alias bb-routes Invoke-BusBuddyRouteOptimization
Set-Alias bb-route-analyze Get-BusBuddyRouteAnalysis
Set-Alias bb-assign-student Start-BusBuddyStudentAssignment

# Export functions
Export-ModuleMember -Function Invoke-BusBuddyRouteOptimization, Get-BusBuddyRouteAnalysis, Start-BusBuddyStudentAssignment
Export-ModuleMember -Alias bb-routes, bb-route-analyze, bb-assign-student
```

---

## 🧪 **Testing Patterns**

### **Route Optimization Service Tests**

```csharp
// BusBuddy.Tests/Core/GrokRouteOptimizationServiceTests.cs
[TestFixture]
[Category("Integration")]
public class GrokRouteOptimizationServiceTests
{
    private GrokRouteOptimizationService _service;
    private Mock<HttpMessageHandler> _httpHandlerMock;
    private HttpClient _httpClient;
    private IConfiguration _configuration;
    private ILogger<GrokRouteOptimizationService> _logger;

    [SetUp]
    public void Setup()
    {
        _httpHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpHandlerMock.Object);

        var configValues = new Dictionary<string, string>
        {
            {"XAI:ApiKey", "test-api-key"},
            {"XAI:BaseUrl", "https://api.x.ai/v1"}
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        _logger = Substitute.For<ILogger<GrokRouteOptimizationService>>();
        _service = new GrokRouteOptimizationService(_httpClient, _configuration, _logger);
    }

    [Test]
    public async Task OptimizeRoutesAsync_ValidInput_ReturnsOptimizedRoutes()
    {
        // Arrange
        var students = CreateTestStudents();
        var buses = CreateTestBuses();
        var drivers = CreateTestDrivers();

        var grokResponse = CreateMockGrokResponse();
        SetupHttpMock(grokResponse);

        // Act
        var result = await _service.OptimizeRoutesAsync(students, buses, drivers);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Routes.Count, Is.GreaterThan(0));
        Assert.That(result.Summary.TotalStudents, Is.EqualTo(students.Count));
    }

    [Test]
    public async Task AssignStudentToOptimalRouteAsync_ValidStudent_ReturnsRecommendation()
    {
        // Arrange
        var student = CreateTestStudent();
        var routes = CreateTestRoutes();

        var grokResponse = CreateMockAssignmentResponse();
        SetupHttpMock(grokResponse);

        // Act
        var result = await _service.AssignStudentToOptimalRouteAsync(student, routes);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RecommendedRouteId, Is.GreaterThan(0));
        Assert.That(result.ConfidenceScore, Is.InRange(0.0, 1.0));
        Assert.That(result.Reasoning, Is.Not.Empty);
    }

    private List<Student> CreateTestStudents()
    {
        return new List<Student>
        {
            new() { StudentId = 1, FirstName = "John", LastName = "Doe", Grade = 5, Address = "123 Main St" },
            new() { StudentId = 2, FirstName = "Jane", LastName = "Smith", Grade = 3, Address = "456 Oak Ave" },
            new() { StudentId = 3, FirstName = "Bob", LastName = "Johnson", Grade = 7, Address = "789 Pine Rd" }
        };
    }

    private void SetupHttpMock(string responseContent)
    {
        _httpHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });
    }
}
```

---

## 📋 **Quick Reference**

### **Key Grok API Features for Route Optimization**

- **Route Optimization**: Analyze student addresses and bus capacities for optimal assignments
- **Student Assignment**: Get AI recommendations for individual student-to-route assignments
- **Efficiency Analysis**: Evaluate existing routes for improvement opportunities
- **Real-time Suggestions**: Interactive assignment suggestions during manual operations

### **Commands for Route Management**

```powershell
# Optimize all routes with AI
bb-routes -OptimizationType All

# Analyze specific route efficiency
bb-route-analyze -RouteId 5

# Get assignment suggestion for student
bb-assign-student -StudentId 123

# Load route assignment reference
bb-copilot-ref Route-Assignment-Logic
```

### **Integration Points**

- **xAI Grok API**: Route optimization and assignment suggestions
- **Entity Framework**: Route, Student, Bus, and Driver data management
- **Syncfusion WPF**: Professional UI components for route management
- **PowerShell Automation**: Command-line tools for batch operations

---

**📋 Note**: This reference provides GitHub Copilot with comprehensive patterns for route assignment and xAI Grok integration in BusBuddy. Use `bb-copilot-ref Route-Assignment-Logic` to load these patterns before implementing route features.
