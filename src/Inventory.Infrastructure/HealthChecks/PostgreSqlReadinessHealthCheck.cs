using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Inventory.Infrastructure.HealthChecks;

/// <summary>
/// Readiness probe: can the API actually reach its database right now?
/// </summary>
/// <remarks>
/// Opens a raw Npgsql connection and issues <c>SELECT 1</c>. It deliberately uses no
/// <c>DbContext</c>, no entity and no migration, because none exists until Phase 2 —
/// this is a connectivity probe, not data access (docs/32 CR-099).
/// <para>
/// Until PostgreSQL 16+ is installed this reports <see cref="HealthStatus.Unhealthy"/>,
/// which is the truthful answer. A readiness probe that reported healthy without a
/// reachable database would be worse than no probe at all.
/// </para>
/// <para>
/// The failure description never includes the connection string, the server host, or the
/// provider's exception text — a readiness endpoint is unauthenticated (docs/32 CR-064),
/// so it must disclose liveness and nothing else. Details go to the log, not the response.
/// </para>
/// </remarks>
public sealed class PostgreSqlReadinessHealthCheck : IHealthCheck
{
    private readonly string? _connectionString;

    /// <summary>Creates the probe for the supplied connection string.</summary>
    /// <param name="connectionString">
    /// The configured connection string, or <see langword="null"/>/empty when none is configured yet.
    /// </param>
    public PostgreSqlReadinessHealthCheck(string? connectionString)
    {
        _connectionString = connectionString;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return HealthCheckResult.Unhealthy("Database connection string is not configured.");
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            _ = await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Database reachable.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The exception is carried for the logger; the description stays generic.
            return HealthCheckResult.Unhealthy("Database is not reachable.", exception);
        }
    }
}
