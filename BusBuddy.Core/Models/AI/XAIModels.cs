using System;
using System.Collections.Generic;

namespace BusBuddy.Core.Models
{
    #region XAI API Models

    public class XAIRequest
    {
        public string Model { get; set; } = string.Empty;
        public XAIMessage[] Messages { get; set; } = Array.Empty<XAIMessage>();
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 4000;
    }

    public class XAIMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class XAIResponse
    {
        public XAIChoice[] Choices { get; set; } = Array.Empty<XAIChoice>();
        public XAIUsage Usage { get; set; } = new();
    }

    public class XAIChoice
    {
        public XAIMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }

    public class XAIUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    #endregion

    #region Request Models

    public class RouteAnalysisRequest
    {
        public string RouteId { get; set; } = string.Empty;
        public double CurrentDistance { get; set; }
        public int StudentCount { get; set; }
        public int VehicleCapacity { get; set; }
        public TerrainData? TerrainData { get; set; }
        public WeatherData? WeatherData { get; set; }
        public TrafficData? TrafficData { get; set; }
        public HistoricalData? HistoricalData { get; set; }
    }

    public class MaintenanceAnalysisRequest
    {
        public string BusId { get; set; } = string.Empty;
        public string VehicleMake { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public int VehicleYear { get; set; }
        public int CurrentMileage { get; set; }
        public DateTime LastMaintenanceDate { get; set; }
        public double DailyMiles { get; set; }
        public string TerrainDifficulty { get; set; } = string.Empty;
        public int StopFrequency { get; set; }
        public double ElevationChanges { get; set; }
        public int EngineHours { get; set; }
        public string BrakeUsage { get; set; } = string.Empty;
        public string TireCondition { get; set; } = string.Empty;
        public string FluidLevels { get; set; } = string.Empty;
    }

    public class RouteOptimizationRequest
    {
        public string RouteId { get; set; } = string.Empty;
        public string CurrentPerformance { get; set; } = string.Empty;
        public string TargetMetrics { get; set; } = string.Empty;
        public List<string> Constraints { get; set; } = new();
        public double CurrentEfficiency { get; set; }
        public int StudentsServed { get; set; }
        public double DistanceTraveled { get; set; }
        public TimeSpan AverageTime { get; set; }
    }

    public class MaintenancePredictionRequest
    {
        public string VehicleId { get; set; } = string.Empty;
        public int CurrentMileage { get; set; }
        public DateTime LastServiceDate { get; set; }
        public List<string> PerformanceIssues { get; set; } = new();
        public int AgeInYears { get; set; }
        public string MaintenanceHistory { get; set; } = string.Empty;
        public string CriticalSystems { get; set; } = string.Empty;
    }

    public class SafetyAnalysisRequest
    {
        public string RouteType { get; set; } = string.Empty;
        public string TrafficDensity { get; set; } = string.Empty;
        public string RoadConditions { get; set; } = string.Empty;
        public string WeatherConditions { get; set; } = string.Empty;
        public List<string> AgeGroups { get; set; } = new();
        public int SpecialNeedsCount { get; set; }
        public int TotalStudents { get; set; }
        public int PreviousIncidents { get; set; }
        public int NearMissReports { get; set; }
        public string DriverSafetyRecord { get; set; } = string.Empty;
    }

    public class StudentOptimizationRequest
    {
        public int TotalStudents { get; set; }
        public int AvailableBuses { get; set; }
        public string GeographicConstraints { get; set; } = string.Empty;
        public string SpecialRequirements { get; set; } = string.Empty;
    }


    public class DevelopmentStateRequest
    {
        public string ComponentName { get; set; } = string.Empty;
        public List<string> TechnologyStack { get; set; } = new();
        public string ComplexityLevel { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public int FilesChanged { get; set; }
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public int MethodsCount { get; set; }
        public int ClassesCount { get; set; }
        public int RecentCommits { get; set; }
        public int BugReports { get; set; }
        public int FeatureRequests { get; set; }
        public int PerformanceIssues { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<string> CurrentChallenges { get; set; } = new();
    }

    public class PerformanceDataRequest
    {
        public string ApplicationName { get; set; } = string.Empty;
        public double BuildTime { get; set; }
        public double StartupTime { get; set; }
        public int MemoryUsage { get; set; }
        public double CpuUsage { get; set; }
        public List<string> PerformanceMetrics { get; set; } = new();
    }

    public class MockDataRequest
    {
        public string DataType { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public List<string> RequiredFields { get; set; } = new();
        public string DataFormat { get; set; } = string.Empty;
    }

    public class HelpRequest
    {
        public string Topic { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string UserLevel { get; set; } = string.Empty;
    }

    #endregion

    #region Response Models

    public class AIRouteRecommendations
    {
        public RouteRecommendation OptimalRoute { get; set; } = new();
        public RiskAssessment RiskAssessment { get; set; } = new();
        public double ConfidenceLevel { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public class RouteRecommendation
    {
        public double EstimatedFuelSavings { get; set; }
        public double EstimatedTimeSavings { get; set; }
        public double SafetyScore { get; set; }
        public string[] RecommendedChanges { get; set; } = Array.Empty<string>();
    }

    public class RiskAssessment
    {
        public string OverallRiskLevel { get; set; } = string.Empty;
        public string[] IdentifiedRisks { get; set; } = Array.Empty<string>();
        public string[] MitigationStrategies { get; set; } = Array.Empty<string>();
    }

    public class AIMaintenancePrediction
    {
        public DateTime PredictedMaintenanceDate { get; set; }
        public ComponentPrediction[] ComponentPredictions { get; set; } = Array.Empty<ComponentPrediction>();
        public decimal TotalEstimatedCost { get; set; }
        public decimal PotentialSavings { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<string> ActionableRecommendations { get; set; } = new();
    }

    public class ComponentPrediction
    {
        public string Component { get; set; } = string.Empty;
        public DateTime PredictedWearDate { get; set; }
        public double ConfidenceLevel { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class AISafetyAnalysis
    {
        public double OverallSafetyScore { get; set; }
        public SafetyRiskFactor[] RiskFactors { get; set; } = Array.Empty<SafetyRiskFactor>();
        public string[] Recommendations { get; set; } = Array.Empty<string>();
        public string ComplianceStatus { get; set; } = string.Empty;
        public double ConfidenceLevel { get; set; }
        public List<IdentifiedRisk> IdentifiedRisks { get; set; } = new();
        public List<string> MitigationStrategies { get; set; } = new();
        public string Reasoning { get; set; } = string.Empty;
        public double OverallRiskScore { get; set; }
    }

    public class IdentifiedRisk
    {
        public string? Description { get; set; }
        public string? Severity { get; set; }
    }

    public class SafetyRiskFactor
    {
        public string Factor { get; set; } = string.Empty;
        public string RiskLevel { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Mitigation { get; set; } = string.Empty;
    }

    public class AIStudentOptimization
    {
        public StudentAssignment[] OptimalAssignments { get; set; } = Array.Empty<StudentAssignment>();
        public EfficiencyMetrics EfficiencyGains { get; set; } = new();
        public double ConfidenceLevel { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public class StudentAssignment
    {
        public int BusId { get; set; }
        public int StudentsAssigned { get; set; }
        public double CapacityUtilization { get; set; }
        public double AverageRideTime { get; set; }
    }

    public class EfficiencyMetrics
    {
        public double TotalTimeSaved { get; set; }
        public double FuelSavings { get; set; }
        public double CapacityOptimization { get; set; }
    }


    public class DevelopmentInsights
    {
        public string ComponentHealth { get; set; } = string.Empty;
        public string VelocityAssessment { get; set; } = string.Empty;
        public string[] RiskAreas { get; set; } = Array.Empty<string>();
        public string[] OptimizationOpportunities { get; set; } = Array.Empty<string>();
        public string[] NextSteps { get; set; } = Array.Empty<string>();
        public string[] ResourceRequirements { get; set; } = Array.Empty<string>();
    }

    public class PerformanceAnalysis
    {
        public string OverallPerformance { get; set; } = string.Empty;
        public string[] BottleneckAreas { get; set; } = Array.Empty<string>();
        public string[] OptimizationSuggestions { get; set; } = Array.Empty<string>();
        public double PerformanceScore { get; set; }
    }

    public class GeneratedDataSet
    {
        public string DataType { get; set; } = string.Empty;
        public int RecordCount { get; set; }
        public object[] Records { get; set; } = Array.Empty<object>();
        public string Format { get; set; } = string.Empty;
    }

    public class ContextualHelp
    {
        public string Topic { get; set; } = string.Empty;
        public string HelpContent { get; set; } = string.Empty;
        public string[] RelatedTopics { get; set; } = Array.Empty<string>();
        public string[] ActionableSteps { get; set; } = Array.Empty<string>();
    }

    #endregion

    #region Supporting Data Models

    public class TerrainData
    {
        public double MinElevation { get; set; }
        public double MaxElevation { get; set; }
        public double AverageSlope { get; set; }
        public string TerrainType { get; set; } = string.Empty;
        public string RouteDifficulty { get; set; } = string.Empty;
    }

    public class WeatherData
    {
        public string Condition { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public double Visibility { get; set; }
        public string WindCondition { get; set; } = string.Empty;
    }

    public class TrafficData
    {
        public string OverallCondition { get; set; } = string.Empty;
        public List<string> CongestionAreas { get; set; } = new();
        public double AverageSpeed { get; set; }
    }

    public class HistoricalData
    {
        public double AverageFuelConsumption { get; set; }
        public double OnTimePerformance { get; set; }
        public int SafetyIncidents { get; set; }
        public List<string> CommonIssues { get; set; } = new();
    }

    #endregion
}
