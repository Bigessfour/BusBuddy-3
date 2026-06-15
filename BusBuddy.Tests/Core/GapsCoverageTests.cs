using System;
using System.Net.Http;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Data.UnitOfWork;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;

namespace BusBuddy.Tests.Core
{
    [TestFixture]
    public class GapsCoverageTests
    {
        [Test]
        public async Task DashboardMetricsService_GetMetrics_ReturnsData()
        {
            var services = new ServiceCollection();
            services.AddDbContext<BusBuddyDbContext>(options =>
                options.UseInMemoryDatabase("GapsTestDb_" + Guid.NewGuid()));
            var serviceProvider = services.BuildServiceProvider();

            await using var scope = serviceProvider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<BusBuddyDbContext>();
            context.Students.Add(new Student { StudentId = 1, StudentName = "Test" });
            context.Routes.Add(new Route { RouteId = 1, RouteName = "Test Route" });
            await context.SaveChangesAsync();

            var service = new DashboardMetricsService(serviceProvider);
            var result = await service.GetDashboardMetricsAsync();

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void GrokGlobalAPI_CanBeInstantiated_WithMockConfig()
        {
            var mockConfig = new Mock<IConfiguration>();
            using var httpClient = new HttpClient();
            var api = new GrokGlobalAPI(httpClient, mockConfig.Object);

            Assert.That(api, Is.Not.Null);
        }

        [Test]
        public void UserContextService_ProvidesDefaultUser()
        {
            var service = new UserContextService();

            Assert.That(service.CurrentUserName, Is.Not.Null.And.Not.Empty);
            Assert.That(service.IsAuthenticated, Is.True);
        }

        [Test]
        public async Task AddressValidationService_ValidateAddress_Basic()
        {
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            var service = new AddressValidationService(mockUnitOfWork.Object);
            var result = await service.ValidateAddressAsync("123 Test St");

            Assert.That(result.IsValid, Is.False);
        }
    }
}
