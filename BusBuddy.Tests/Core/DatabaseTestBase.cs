using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using System.Linq;
using BusBuddy.Core.Services.Interfaces; // Add this line

namespace BusBuddy.Tests.Core
{
    /// <summary>
    /// Base class for integration tests requiring database access
    /// Provides isolated, in-memory SQLite database for each test
    /// </summary>
    public abstract class DatabaseTestBase : IDisposable
    {
        protected BusBuddyDbContext Context { get; private set; } = null!;
        protected IBusBuddyDbContextFactory ContextFactory { get; private set; } = null!;
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        private SqliteConnection? _connection;
        private string? _oldBusBuddyConnection;
        private string? _oldAzureSqlUser;
        private string? _oldAzureSqlPassword;

        [SetUp]
        public virtual void SetUp()
        {
            // Create in-memory SQLite database for each test
            var connectionString = "Data Source=:memory:";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            // Clear environment variables to prevent production DB access
            _oldBusBuddyConnection = Environment.GetEnvironmentVariable("BUSBUDDY_CONNECTION");
            _oldAzureSqlUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
            _oldAzureSqlPassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_USER", null);
            Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", null);

            // Configure test services
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "InMemory",
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["ConnectionStrings:TestConnection"] = connectionString
                })
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<BusBuddyDbContext>(options =>
                options.UseSqlite(_connection));
            services.AddScoped<IBusBuddyDbContextFactory, BusBuddyDbContextFactory>();
            services.AddScoped<IStudentService, StudentService>();
            services.AddScoped<IBusService, BusService>();

            ServiceProvider = services.BuildServiceProvider();
            ContextFactory = ServiceProvider.GetRequiredService<IBusBuddyDbContextFactory>();
            Context = ContextFactory.CreateDbContext();

            // Create database schema
            Context.Database.EnsureCreated();

            // Seed test data
            SeedTestData();
        }

        protected virtual void SeedTestData()
        {
            // Add test routes
            if (!Context.Routes.Any())
            {
                Context.Routes.AddRange(
                    new Route
                    {
                        RouteId = 1,
                        RouteName = "Route A",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Test School",
                        Boundaries = "Test Area A"
                    },
                    new Route
                    {
                        RouteId = 2,
                        RouteName = "Route B", 
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Test School",
                        Boundaries = "Test Area B"
                    },
                    new Route
                    {
                        RouteId = 3,
                        RouteName = "East Route",
                        Date = DateTime.Today,
                        IsActive = true,
                        School = "Wiley School District",
                        Boundaries = "east of 287"
                    }
                );
            }

            // Add test buses
            if (!Context.Buses.Any())
            {
                Context.Buses.AddRange(
                    new Bus
                    {
                        BusNumber = "17",
                        SeatingCapacity = 48,
                        Status = "Active",
                        Make = "Blue Bird",
                        Model = "Vision",
                        Year = 2020
                    },
                    new Bus
                    {
                        BusNumber = "EAST1",
                        SeatingCapacity = 48,
                        Status = "Active",
                        Make = "East Route",
                        Model = "RouteBus",
                        Year = 2020
                    }
                );
            }

            // Add test students
            if (!Context.Students.Any())
            {
                Context.Students.Add(new Student
                {
                    StudentName = "Test Student",
                    HomeAddress = "123 Test St",
                    City = "Test City",
                    State = "CO",
                    Zip = "12345",
                    Grade = "5",
                    School = "Test School",
                    Active = true,
                    EnrollmentDate = DateTime.Today,
                    CreatedDate = DateTime.Now
                });
            }

            // Add test route stops
            if (!Context.RouteStops.Any())
            {
                Context.RouteStops.AddRange(
                    new RouteStop 
                    { 
                        RouteId = 1,
                        StopName = "Oak & 1st", 
                        StopAddress = "Oak Street & 1st Avenue", 
                        StopOrder = 1,
                        ScheduledArrival = new TimeSpan(7, 30, 0),
                        ScheduledDeparture = new TimeSpan(7, 32, 0),
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow
                    },
                    new RouteStop 
                    { 
                        RouteId = 1,
                        StopName = "Maple & Main", 
                        StopAddress = "Maple Street & Main Street", 
                        StopOrder = 2,
                        ScheduledArrival = new TimeSpan(7, 35, 0),
                        ScheduledDeparture = new TimeSpan(7, 37, 0),
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow
                    },
                    new RouteStop 
                    { 
                        RouteId = 2,
                        StopName = "Pine & Center", 
                        StopAddress = "Pine Street & Center Avenue", 
                        StopOrder = 1,
                        ScheduledArrival = new TimeSpan(7, 40, 0),
                        ScheduledDeparture = new TimeSpan(7, 42, 0),
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow
                    }
                );
            }

            Context.SaveChanges();
        }

        [TearDown] 
        public virtual void TearDown()
        {
            Context?.Dispose();
            _connection?.Dispose();
            ServiceProvider?.GetService<IServiceScope>()?.Dispose();

            // Restore environment variables
            Environment.SetEnvironmentVariable("BUSBUDDY_CONNECTION", _oldBusBuddyConnection);
            Environment.SetEnvironmentVariable("AZURE_SQL_USER", _oldAzureSqlUser);
            Environment.SetEnvironmentVariable("AZURE_SQL_PASSWORD", _oldAzureSqlPassword);
        }

        public virtual void Dispose()
        {
            TearDown();
            GC.SuppressFinalize(this);
        }
    }
}
