using System.Collections.Generic;
using System.Threading.Tasks;
using BusBuddy.Core.Models;

namespace BusBuddy.WPF.Services
{
    public interface IRoutePopulationScaffold
    {
        Task<List<Route>> GetOptimizedRoutesAsync();
        Task PopulateRoutesAsync();
        Task PopulateRouteMetadataAsync();
    }
}
