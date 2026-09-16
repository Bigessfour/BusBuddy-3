using System;
using System.Collections.Generic;

namespace BusBuddy.Core.Models
{
    /// <summary>
    /// OpenAI-compatible chat request used against local Ollama.
    /// </summary>
    public class ChatCompletionRequest
    {
        public string Model { get; set; } = string.Empty;
        public ChatCompletionMessage[] Messages { get; set; } = Array.Empty<ChatCompletionMessage>();
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 4000;
    }

    public class ChatCompletionMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ChatCompletionResponse
    {
        public ChatCompletionChoice[] Choices { get; set; } = Array.Empty<ChatCompletionChoice>();
        public ChatCompletionUsage Usage { get; set; } = new();
    }

    public class ChatCompletionChoice
    {
        public ChatCompletionMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }

    public class ChatCompletionUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
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
}
