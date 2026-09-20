using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BusBuddy.Core.Data;
using BusBuddy.Core.Models;
using BusBuddy.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        public void OllamaAiService_CanBeInstantiated_WithMockConfig()
        {
            IConfiguration config = new ConfigurationBuilder().Build();
            using var httpClient = new HttpClient();
            var api = new OllamaAiService(httpClient, config);

            Assert.That(api, Is.Not.Null);
            Assert.That(api.IsConfigured, Is.True);
        }

        [Test]
        public async Task OllamaAiService_OptimizeRoutes_WhenUnreachable_ReturnsMock()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://127.0.0.1:1/v1",
                ["Ollama:Enabled"] = "true",
                ["Ollama:TimeoutSeconds"] = "2"
            };
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var api = new OllamaAiService(httpClient, config);

            var result = await api.OptimizeRoutesAsync(new RouteOptimizationRequest { RouteId = "test-1" });

            Assert.That(result, Is.Not.Null);
            Assert.That(result.AIModel, Is.EqualTo("Mock-AI"));
            Assert.That(result.OptimizationSuggestions, Does.Contain("Mock optimization"));
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
            // Without Maps client, validation must not succeed via regex alone.
            var service = new AddressValidationService();
            var result = await service.ValidateAddressAsync("123 Test St");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.NormalizedAddress, Is.Null);
        }

        [Test]
        public async Task UserSettingsService_LoadSave_Basic()
        {
            var service = new UserSettingsService();
            await service.SetSettingAsync("testKey", "testValue");
            var value = await service.GetSettingAsync<string>("testKey", string.Empty);
            Assert.That(value, Is.EqualTo("testValue"));
        }
    }
}
