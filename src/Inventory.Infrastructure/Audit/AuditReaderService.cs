using Inventory.Application.Audit;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Audit;

/// <summary>See <see cref="IAuditReaderService"/>.</summary>
public sealed class AuditReaderService : IAuditReaderService
{
    /// <summary>Task 14.3 (docs/17 §1.3) - the bound applied when the caller supplies no
    /// explicit <see cref="AuditLogFilter.From"/>.</summary>
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(30);

    private readonly InventoryDbContext _context;
    private readonly IAuditLogger _auditLogger;

    public AuditReaderService(InventoryDbContext context, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<KeysetPage<AuditLogSummary>> ListAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPage<AuditLog> page = await QueryAsync(filter, limit, cursor, cancellationToken);
        return new KeysetPage<AuditLogSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<KeysetPage<AuditActivitySummary>> ListActivityAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPage<AuditLog> page = await QueryAsync(filter, limit, cursor, cancellationToken);
        return new KeysetPage<AuditActivitySummary>(
            page.Items.Select(a => new AuditActivitySummary(a.Id, a.DescriptionArabic, a.ActorRole, a.Result.ToString(), a.CreatedAt)).ToList(),
            page.NextCursor);
    }

    public async Task<KeysetPage<AuditLogSummary>> ExportAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPage<AuditLog> page = await QueryAsync(filter, limit, cursor, cancellationToken);

        // Task 14.4: the export act itself is audited - a distinct entry from any of the rows it
        // just read, written and saved directly here since export has no other business
        // transaction to ride along with.
        _auditLogger.Record(new AuditEntry(
            "AUDIT_LOG_EXPORTED", nameof(AuditLog), Guid.Empty,
            $"تم تصدير سجل التدقيق ({page.Items.Count} سجل)",
            OldValues: null, NewValues: new { RowCount = page.Items.Count }, Domain.Enums.AuditResult.Success));
        await _context.SaveChangesAsync(cancellationToken);

        return new KeysetPage<AuditLogSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    private async Task<KeysetPage<AuditLog>> QueryAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        DateTimeOffset from = filter.From ?? DateTimeOffset.UtcNow - DefaultWindow;
        DateTimeOffset to = filter.To ?? DateTimeOffset.UtcNow;

        IQueryable<AuditLog> query = _context.AuditLogs.AsNoTracking()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to);

        if (filter.ActorUserId is { } actorUserId)
        {
            query = query.Where(a => a.ActorUserId == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        query = query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<AuditLog> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        return KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);
    }

    private static AuditLogSummary ToSummary(AuditLog log) => new(
        log.Id, log.ActorUserId, log.ActorRole, log.Action, log.EntityType, log.EntityId,
        log.DescriptionArabic, log.OldValuesJson, log.NewValuesJson, log.Result.ToString(),
        log.WarehouseId, log.RestaurantId, log.CorrelationId, log.CreatedAt);
}
