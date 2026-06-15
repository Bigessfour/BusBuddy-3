using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusBuddy.Core.Services
{
    public class Phase1DataSeedingService
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly ILogger Logger = Log.ForContext<Phase1DataSeedingService>();

        public Phase1DataSeedingService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task SeedDataAsync()
        {
            Logger.Information("Starting Phase 1 data seeding...");
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BusBuddy.Core.Data.BusBuddyDbContext>();

            await context.Database.EnsureCreatedAsync();

            if (!await context.Students.AnyAsync())
            {
                await SeedStudentsAsync(context);
            }

            if (!await context.Routes.AnyAsync())
            {
                await SeedRoutesAsync(context);
            }

            Logger.Information("Phase 1 data seeding complete.");
        }

        private async Task SeedStudentsAsync(BusBuddyDbContext context)
        {
            var students = new List<Student>
            {
                new() { StudentName = "Peter Pan", Grade = "4" },
                new() { StudentName = "Wendy Darling", Grade = "4" },
                new() { StudentName = "Tinker Bell", Grade = "1" }
            };
            await context.Students.AddRangeAsync(students);
            await context.SaveChangesAsync();
            Logger.Information("Seeded {Count} students for Phase 1", students.Count);
        }

        private async Task SeedRoutesAsync(BusBuddyDbContext context)
        {
            var routes = new List<Route>
            {
                new() { RouteName = "Neverland Express", School = "Default School" },
                new() { RouteName = "Lost Boys Shuttle", School = "Default School" }
            };
            await context.Routes.AddRangeAsync(routes);
            await context.SaveChangesAsync();
            Logger.Information("Seeded {Count} routes for Phase 1", routes.Count);
        }
    }
}
