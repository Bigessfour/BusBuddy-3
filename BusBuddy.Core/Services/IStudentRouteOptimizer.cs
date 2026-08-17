using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    /// <summary>
    /// Assigns unassigned students to active routes and optionally asks the local AI for commentary.
    /// </summary>
    public interface IStudentRouteOptimizer
    {
        Task<StudentRouteOptimizeResult> OptimizeUnassignedAsync();
    }

    public sealed class StudentRouteOptimizeResult
    {
        public int AssignedCount { get; init; }
        public int RemainingUnassigned { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? AiSummary { get; init; }
        public bool UsedMockAi { get; init; }
    }
}
