using BusBuddy.Core.Models;

namespace BusBuddy.Core.Data.Interfaces;

/// <summary>
/// Route repository. Route-specific queries live on <c>IRouteService</c>; this type exists so
/// <c>IUnitOfWork.Routes</c> can expose the generic CRUD surface.
/// </summary>
public interface IRouteRepository : IRepository<Route>
{
}
