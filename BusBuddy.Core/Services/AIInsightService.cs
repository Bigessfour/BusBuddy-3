using System.Text.Json;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BusBuddy.Core.Services;

/// <summary>
/// Service for managing AI insights from Grok analysis and storing them in Azure SQL Database
/// Follows Azure best practices for secure data storage and retrieval
/// </summary>
public class AIInsightService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AIInsightService>();
    private readonly BusBuddyDbContext _context;
    private readonly GrokGlobalAPI _grokApi;
    private readonly IConfiguration _configuration;

    public AIInsightService(BusBuddyDbContext context, GrokGlobalAPI grokApi, IConfiguration configuration)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _grokApi = grokApi ?? throw new ArgumentNullException(nameof(grokApi));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Store maintenance prediction insights from Grok analysis
    /// Aligns with PowerShell: Invoke-GrokMaintenancePrediction
    /// </summary>
    public async Task<AIInsight> StoreMaintenanceInsightAsync(int vehicleId, string analysisResult, decimal confidenceScore, string createdBy = "System")
    {
        try
        {
            Logger.Information("Storing maintenance insight for vehicle {VehicleId}", vehicleId);

            var insight = new AIInsight
            {
                InsightType = "Maintenance",
                Priority = DeterminePriorityFromConfidence(confidenceScore),
                EntityReference = $"Vehicle_{vehicleId}",
                VehicleId = vehicleId,
                InsightDetails = analysisResult,
                Summary = ExtractSummaryFromAnalysis(analysisResult, "Maintenance"),
                RecommendedActions = ExtractRecommendationsFromAnalysis(analysisResult),
                ConfidenceScore = confidenceScore,
                Source = "Grok-4",
                Status = "New",
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(30), // Maintenance insights expire in 30 days
                Tags = "maintenance,prediction,vehicle"
            };

            _context.AIInsights.Add(insight);
            await _context.SaveChangesAsync();

            Logger.Information("Maintenance insight {InsightId} stored successfully for vehicle {VehicleId}", insight.InsightId, vehicleId);
            return insight;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to store maintenance insight for vehicle {VehicleId}", vehicleId);
            throw;
        }
    }

    /// <summary>
    /// Store route optimization insights from Grok analysis
    /// Aligns with PowerShell: Invoke-GrokRouteOptimization
    /// </summary>
    public async Task<AIInsight> StoreRouteOptimizationInsightAsync(int routeId, string optimizationResult, decimal confidenceScore, decimal? estimatedSavings = null, string createdBy = "System")
    {
        try
        {
            Logger.Information("Storing route optimization insight for route {RouteId}", routeId);

            var insight = new AIInsight
            {
                InsightType = "Route",
                Priority = DeterminePriorityFromConfidence(confidenceScore),
                EntityReference = $"Route_{routeId}",
                RouteId = routeId,
                InsightDetails = optimizationResult,
                Summary = ExtractSummaryFromAnalysis(optimizationResult, "Route Optimization"),
                RecommendedActions = ExtractRecommendationsFromAnalysis(optimizationResult),
                ConfidenceScore = confidenceScore,
                Source = "Grok-4",
                Status = "New",
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                EstimatedSavings = estimatedSavings,
                ExpiryDate = DateTime.UtcNow.AddDays(14), // Route optimizations expire in 14 days
                Tags = "route,optimization,efficiency"
            };

            _context.AIInsights.Add(insight);
            await _context.SaveChangesAsync();

            Logger.Information("Route optimization insight {InsightId} stored successfully for route {RouteId} with estimated savings ${EstimatedSavings}",
                insight.InsightId, routeId, estimatedSavings);
            return insight;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to store route optimization insight for route {RouteId}", routeId);
            throw;
        }
    }

    /// <summary>
    /// Store UI/UX optimization insights from Grok analysis
    /// Aligns with PowerShell: Invoke-GrokUI_UXReview
    /// </summary>
    public async Task<AIInsight> StoreUIOptimizationInsightAsync(string xamlContent, string optimizationResult, decimal confidenceScore, string createdBy = "System")
    {
        try
        {
            Logger.Information("Storing UI optimization insight for XAML content length {XamlLength}", xamlContent.Length);

            var insight = new AIInsight
            {
                InsightType = "UI",
                Priority = DeterminePriorityFromConfidence(confidenceScore),
                EntityReference = "Syncfusion_UI",
                InsightDetails = optimizationResult,
                Summary = ExtractSummaryFromAnalysis(optimizationResult, "UI Optimization"),
                RecommendedActions = ExtractRecommendationsFromAnalysis(optimizationResult),
                ConfidenceScore = confidenceScore,
                Source = "Grok-4",
                Status = "New",
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(7), // UI insights expire in 7 days
                Tags = "ui,syncfusion,xaml,performance"
            };

            _context.AIInsights.Add(insight);
            await _context.SaveChangesAsync();

            Logger.Information("UI optimization insight {InsightId} stored successfully", insight.InsightId);
            return insight;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to store UI optimization insight");
            throw;
        }
    }

    /// <summary>
    /// Store CI/CD pipeline insights from build failures
    /// Aligns with PowerShell: Invoke-GrokCIAnalysis
    /// </summary>
    public async Task<AIInsight> StoreCIInsightAsync(string errorLog, string analysisResult, decimal confidenceScore, string createdBy = "CI/CD")
    {
        try
        {
            Logger.Information("Storing CI/CD insight for error log length {ErrorLogLength}", errorLog.Length);

            var insight = new AIInsight
            {
                InsightType = "CI",
                Priority = "High", // CI failures are always high priority
                EntityReference = "Pipeline",
                InsightDetails = analysisResult,
                Summary = ExtractSummaryFromAnalysis(analysisResult, "CI/CD Analysis"),
                RecommendedActions = ExtractRecommendationsFromAnalysis(analysisResult),
                ConfidenceScore = confidenceScore,
                Source = "Grok-4",
                Status = "New",
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(3), // CI insights expire quickly
                Tags = "ci,build,deployment,pipeline"
            };

            _context.AIInsights.Add(insight);
            await _context.SaveChangesAsync();

            Logger.Information("CI/CD insight {InsightId} stored successfully", insight.InsightId);
            return insight;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to store CI/CD insight");
            throw;
        }
    }

    /// <summary>
    /// Retrieve insights by type with optional filtering
    /// </summary>
    public async Task<List<AIInsight>> GetInsightsByTypeAsync(string insightType, string? status = null, int maxResults = 50)
    {
        try
        {
            var query = _context.AIInsights
                .Where(i => i.InsightType == insightType)
                .Include(i => i.Vehicle)
                .Include(i => i.Route)
                .Include(i => i.Driver)
                .OrderByDescending(i => i.CreatedDate);

            if (!string.IsNullOrEmpty(status))
            {
                query = (IOrderedQueryable<AIInsight>)query.Where(i => i.Status == status);
            }

            var insights = await query.Take(maxResults).ToListAsync();
            
            Logger.Information("Retrieved {Count} insights of type {InsightType}", insights.Count, insightType);
            return insights;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve insights by type {InsightType}", insightType);
            throw;
        }
    }

    /// <summary>
    /// Get high-priority actionable insights for dashboard display
    /// </summary>
    public async Task<List<AIInsight>> GetActionableInsightsAsync(int maxResults = 10)
    {
        try
        {
            var insights = await _context.AIInsights
                .Where(i => i.Status == "New" && (i.Priority == "Critical" || i.Priority == "High"))
                .Where(i => i.ExpiryDate == null || i.ExpiryDate > DateTime.UtcNow)
                .Include(i => i.Vehicle)
                .Include(i => i.Route)
                .Include(i => i.Driver)
                .OrderByDescending(i => i.Priority)
                .ThenByDescending(i => i.ConfidenceScore)
                .ThenByDescending(i => i.CreatedDate)
                .Take(maxResults)
                .ToListAsync();

            Logger.Information("Retrieved {Count} actionable insights", insights.Count);
            return insights;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve actionable insights");
            throw;
        }
    }

    /// <summary>
    /// Update insight status (e.g., mark as reviewed, in progress, resolved)
    /// </summary>
    public async Task<bool> UpdateInsightStatusAsync(int insightId, string newStatus, string updatedBy)
    {
        try
        {
            var insight = await _context.AIInsights.FindAsync(insightId);
            if (insight == null)
            {
                Logger.Warning("Insight {InsightId} not found for status update", insightId);
                return false;
            }

            insight.Status = newStatus;
            insight.UpdatedBy = updatedBy;
            insight.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            Logger.Information("Insight {InsightId} status updated to {NewStatus} by {UpdatedBy}", insightId, newStatus, updatedBy);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to update insight {InsightId} status", insightId);
            throw;
        }
    }

    /// <summary>
    /// Clean up expired insights to maintain database performance
    /// </summary>
    public async Task<int> CleanupExpiredInsightsAsync()
    {
        try
        {
            var expiredInsights = await _context.AIInsights
                .Where(i => i.ExpiryDate != null && i.ExpiryDate < DateTime.UtcNow)
                .Where(i => i.Status != "Resolved") // Keep resolved insights for historical analysis
                .ToListAsync();

            if (expiredInsights.Any())
            {
                _context.AIInsights.RemoveRange(expiredInsights);
                await _context.SaveChangesAsync();

                Logger.Information("Cleaned up {Count} expired insights", expiredInsights.Count);
            }

            return expiredInsights.Count;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to cleanup expired insights");
            throw;
        }
    }

    #region Private Helper Methods

    private static string DeterminePriorityFromConfidence(decimal confidence)
    {
        return confidence switch
        {
            >= 0.9m => "Critical",
            >= 0.7m => "High",
            >= 0.5m => "Medium",
            _ => "Low"
        };
    }

    private static string ExtractSummaryFromAnalysis(string analysisResult, string context)
    {
        try
        {
            // Try to parse JSON and extract summary
            var json = JsonDocument.Parse(analysisResult);
            if (json.RootElement.TryGetProperty("summary", out var summaryElement))
            {
                return summaryElement.GetString() ?? $"{context} analysis completed";
            }
        }
        catch
        {
            // If not JSON, take first 200 characters as summary
            return analysisResult.Length > 200 ? analysisResult[..200] + "..." : analysisResult;
        }

        return $"{context} analysis completed";
    }

    private static string ExtractRecommendationsFromAnalysis(string analysisResult)
    {
        try
        {
            // Try to parse JSON and extract recommendations
            var json = JsonDocument.Parse(analysisResult);
            if (json.RootElement.TryGetProperty("recommendations", out var recommendationsElement))
            {
                return recommendationsElement.GetString() ?? "See analysis details for recommendations";
            }
        }
        catch
        {
            // If not JSON, look for common recommendation patterns
            if (analysisResult.Contains("recommend", StringComparison.OrdinalIgnoreCase))
            {
                var lines = analysisResult.Split('\n');
                var recommendations = lines.Where(line => line.Contains("recommend", StringComparison.OrdinalIgnoreCase))
                                          .Take(3)
                                          .ToList();
                if (recommendations.Any())
                {
                    return string.Join("; ", recommendations);
                }
            }
        }

        return "See analysis details for recommendations";
    }

    #endregion
}
