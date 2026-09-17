using System.Globalization;
using Inventory.Application.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Idempotency;

/// <summary>
/// Task 7.11 - two unrelated recurring maintenance jobs sharing one timer because both are cheap,
/// infrequent, and need nothing but a DB connection: (1) delete idempotency records past their
/// 24-hour retention (docs/30 §6.3), and (2) pre-create next month's <c>audit_logs</c> partition
/// (docs/29 §4.5) so a write at the very start of a new month never races the partition's own
/// creation. Runs once at startup (so a freshly-deployed instance is never a day behind) and then
/// every 24 hours - daily is far more often than either job strictly needs, but simple, and
/// `CREATE TABLE IF NOT EXISTS` / a delete-by-expiry are both naturally idempotent against
/// running "too often".
/// </summary>
public sealed partial class MaintenanceBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceBackgroundService> _logger;

    public MaintenanceBackgroundService(IServiceScopeFactory scopeFactory, ILogger<MaintenanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed maintenance pass must never crash the host - the next scheduled pass
                // will simply retry the same idempotent work.
                LogMaintenancePassFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Public so an integration test can trigger exactly one pass deterministically,
    /// rather than waiting on <see cref="Interval"/> or racing the host's own background
    /// execution.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var idempotencyService = scope.ServiceProvider.GetRequiredService<IIdempotencyService>();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        int removed = await idempotencyService.CleanupExpiredAsync(cancellationToken);
        LogIdempotencyCleanup(_logger, removed);

        await EnsureNextMonthAuditPartitionAsync(context, cancellationToken);
    }

    /// <summary>Mirrors the exact naming/boundary pattern the initial migration used for the
    /// first two partitions (`audit_logs_YYYY_MM`, `FOR VALUES FROM (month) TO (next month)`) -
    /// `IF NOT EXISTS` makes re-running this on every pass harmless.</summary>
    private async Task EnsureNextMonthAuditPartitionAsync(InventoryDbContext context, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset nextMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        DateTimeOffset followingMonthStart = nextMonthStart.AddMonths(1);

        string partitionName = string.Create(CultureInfo.InvariantCulture, $"audit_logs_{nextMonthStart:yyyy_MM}");
        string fromLiteral = nextMonthStart.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        string toLiteral = followingMonthStart.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // A partition name is a SQL identifier, not a value - Postgres has no way to bind it as
        // a parameter (unlike the two boundary dates, which could be, but gain nothing from it
        // here since every input is server-computed from UtcNow, never from request data).
        // ExecuteSqlRaw is the correct, documented escape hatch for exactly this case.
#pragma warning disable EF1002
        string sql =
            $"""
            CREATE TABLE IF NOT EXISTS {partitionName} PARTITION OF audit_logs
                FOR VALUES FROM ('{fromLiteral}') TO ('{toLiteral}');
            """;
        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002

        LogPartitionEnsured(_logger, partitionName);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Idempotency cleanup removed {Count} expired record(s).")]
    private static partial void LogIdempotencyCleanup(ILogger logger, int count);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Ensured audit_logs partition {PartitionName} exists.")]
    private static partial void LogPartitionEnsured(ILogger logger, string partitionName);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "A maintenance pass failed; will retry on the next scheduled run.")]
    private static partial void LogMaintenancePassFailed(ILogger logger, Exception exception);
}
