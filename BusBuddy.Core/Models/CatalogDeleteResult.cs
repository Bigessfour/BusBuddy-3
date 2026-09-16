namespace BusBuddy.Core.Models;

/// <summary>
/// Restrict-then-retire for catalog rows (schools, pickup stops). Unused rows are removed;
/// rows still referenced stay with the active flag cleared.
/// </summary>
public enum CatalogDeleteResult
{
    NotFound = 0,
    Deleted = 1,
    Retired = 2
}
