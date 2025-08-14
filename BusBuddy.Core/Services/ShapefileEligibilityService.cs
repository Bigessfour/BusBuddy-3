using System;
using System.Threading.Tasks;
using BusBuddy.Core.Services.Interfaces;

namespace BusBuddy.Core.Services
{
    // Deprecated: Shapefile-based eligibility removed. This stub preserves the type for callers
    // while eliminating external geospatial dependencies (NetTopologySuite).
    [Obsolete("Shapefile eligibility is deprecated and removed for MVP; this stub always returns false.")]
    internal sealed class ShapefileEligibilityService : IEligibilityService, IDisposable
    {
        public ShapefileEligibilityService(string districtShpPath, string townShpPath)
        {
            // Intentionally no-op; service is deprecated.
        }

        public Task<bool> IsEligibleAsync(double latitude, double longitude)
        {
            // Eligibility feature disabled; always return false.
            return Task.FromResult(false);
        }

        public void Dispose()
        {
            // Nothing to dispose in stub
        }
    }
}
