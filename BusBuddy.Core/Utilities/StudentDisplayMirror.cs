using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Utilities;

/// <summary>
/// <see cref="Student.School"/>, <see cref="Student.AMRoute"/>, and <see cref="Student.PMRoute"/>
/// are display mirrors. <see cref="Student.DestinationId"/>, <see cref="Student.AmRouteId"/>, and
/// <see cref="Student.PmRouteId"/> are the keys. A blank key may still be filled from a unique name
/// (import and pre-key rows). An existing key is never cleared because its mirror text is stale
/// or ambiguous.
/// </summary>
public static class StudentDisplayMirror
{
    public static async Task SyncAsync(BusBuddyDbContext context, Student student)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(student);
        await SyncRouteSlotAsync(context, student, am: true).ConfigureAwait(false);
        await SyncRouteSlotAsync(context, student, am: false).ConfigureAwait(false);
        await SyncSchoolAsync(context, student).ConfigureAwait(false);
    }

    /// <summary>
    /// Null when the slot is empty, the key exists, or a null key names exactly one route.
    /// </summary>
    public static async Task<string?> DescribeRouteSlotAsync(
        BusBuddyDbContext context,
        int? routeId,
        string? routeName,
        string label)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (routeId is > 0)
        {
            var exists = await context.Routes.AsNoTracking()
                .AnyAsync(r => r.RouteId == routeId)
                .ConfigureAwait(false);
            return exists ? null : $"{label} route id {routeId} does not exist";
        }

        if (string.IsNullOrWhiteSpace(routeName))
        {
            return null;
        }

        var resolved = await UniqueRouteAsync(context, routeName).ConfigureAwait(false);
        return resolved is null
            ? $"{label} route '{routeName.Trim()}' does not exist or is not unique"
            : null;
    }

    private static async Task SyncRouteSlotAsync(BusBuddyDbContext context, Student student, bool am)
    {
        var routeId = am ? student.AmRouteId : student.PmRouteId;
        var routeName = am ? student.AMRoute : student.PMRoute;

        if (routeId is > 0)
        {
            var canonical = await context.Routes.AsNoTracking()
                .Where(r => r.RouteId == routeId)
                .Select(r => r.RouteName)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(canonical))
            {
                if (am)
                {
                    student.AMRoute = canonical;
                }
                else
                {
                    student.PMRoute = canonical;
                }
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(routeName))
        {
            if (am)
            {
                student.AmRouteId = null;
                student.AMRoute = null;
            }
            else
            {
                student.PmRouteId = null;
                student.PMRoute = null;
            }

            return;
        }

        var resolved = await UniqueRouteAsync(context, routeName).ConfigureAwait(false);
        if (resolved is not { } route)
        {
            return;
        }

        if (am)
        {
            student.AmRouteId = route.Id;
            student.AMRoute = route.Name;
        }
        else
        {
            student.PmRouteId = route.Id;
            student.PMRoute = route.Name;
        }
    }

    private static async Task SyncSchoolAsync(BusBuddyDbContext context, Student student)
    {
        if (student.DestinationId is > 0)
        {
            var name = await context.Destinations.AsNoTracking()
                .Where(d => d.DestinationId == student.DestinationId && !d.IsDeleted)
                .Select(d => d.Name)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(name))
            {
                student.School = name;
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(student.School))
        {
            student.School = null;
            return;
        }

        var lower = student.School.Trim().ToLowerInvariant();
#pragma warning disable CA1311, CA1862
        var matches = await context.Destinations.AsNoTracking()
            .Where(d => !d.IsDeleted
                        && d.DestinationType == DestinationTypes.School
                        && d.Name.ToLower() == lower)
            .Select(d => new { d.DestinationId, d.Name })
            .Take(2)
            .ToListAsync()
            .ConfigureAwait(false);
#pragma warning restore CA1311, CA1862
        if (matches.Count != 1)
        {
            return;
        }

        student.DestinationId = matches[0].DestinationId;
        student.School = matches[0].Name;
    }

    private static async Task<(int Id, string Name)?> UniqueRouteAsync(BusBuddyDbContext context, string routeName)
    {
        var nameLower = routeName.Trim().ToLowerInvariant();
#pragma warning disable CA1311, CA1862
        var matches = await context.Routes.AsNoTracking()
            .Where(r => r.RouteName.ToLower() == nameLower)
            .Select(r => new { r.RouteId, r.RouteName })
            .Take(2)
            .ToListAsync()
            .ConfigureAwait(false);
#pragma warning restore CA1311, CA1862
        if (matches.Count != 1 || string.IsNullOrWhiteSpace(matches[0].RouteName))
        {
            return null;
        }

        return (matches[0].RouteId, matches[0].RouteName);
    }
}
