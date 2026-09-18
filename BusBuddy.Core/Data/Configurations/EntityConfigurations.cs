using Microsoft.EntityFrameworkCore;

namespace BusBuddy.Core.Data.Configurations;

internal static class EntityConfigurations
{
    internal static void Apply(ModelBuilder modelBuilder, string? providerName)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(EntityConfigurations).Assembly,
            type => type.Namespace == typeof(EntityConfigurations).Namespace
                    && type != typeof(TripEventConfiguration));
        modelBuilder.ApplyConfiguration(new TripEventConfiguration(providerName));
    }
}
