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

    #endregion
}
