using BusBuddy.Core.Data.Interfaces;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;

namespace BusBuddy.Core.Data.Repositories;

/// <summary>
/// Generic route CRUD. Assignment, stops, and validation go through <c>IRouteService</c>.
/// </summary>
public class RouteRepository : Repository<Route>, IRouteRepository
{
    public RouteRepository(BusBuddyDbContext context, IUserContextService userContextService)
        : base(context, userContextService)
    {
    }
}
