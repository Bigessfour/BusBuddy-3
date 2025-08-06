namespace BusBuddy.Core.Models
{
    public class TerrainAnalysisResult
    {
        public double MinElevation { get; set; }
        public double MaxElevation { get; set; }
        public double AverageSlope { get; set; }
        public string TerrainType { get; set; } = string.Empty;
        public string RouteDifficulty { get; set; } = string.Empty;
    }
}
