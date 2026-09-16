using Inventory.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Inventory.Infrastructure;

/// <summary>
/// The infrastructure composition root.
/// </summary>
/// <remarks>
/// This is the single seam through which <c>Inventory.Api</c> is allowed to reach the
/// infrastructure layer. No Api type may reference an infrastructure type directly
/// (ADR-002), which keeps the dependency direction inward and the host thin.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Name of the readiness health check, used to tag and filter it.</summary>
    public const string DatabaseReadinessCheckName = "postgresql";

    /// <summary>Tag applied to checks that belong to the readiness probe.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>Configuration key holding the primary database connection string.</summary>
    public const string DatabaseConnectionName = "InventoryDatabase";

    /// <summary>
    /// Registers infrastructure services and health checks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string? connectionString = configuration.GetConnectionString(DatabaseConnectionName);

        services.AddHealthChecks()
            .AddCheck(
                name: DatabaseReadinessCheckName,
                instance: new PostgreSqlReadinessHealthCheck(connectionString),
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadinessTag]);

        return services;
    }
}
