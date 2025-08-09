using System.Threading.Tasks;

namespace BusBuddy.Core.Services.Interfaces
{
    /// <summary>
    /// Determines student transportation eligibility based on district and town boundaries.
    /// </summary>
    public interface IEligibilityService
    {
        /// <summary>
        /// Returns true if the coordinate is inside the school district and outside the town boundary.
        /// </summary>
        /// <param name="latitude">WGS84 latitude</param>
        /// <param name="longitude">WGS84 longitude</param>
        Task<bool> IsEligibleAsync(double latitude, double longitude);
    }
}
