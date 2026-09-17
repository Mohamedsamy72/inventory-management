using Inventory.Application.Common;
using Inventory.Application.Discrepancies;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Discrepancies;

/// <summary>See <see cref="IDiscrepancyService"/>.</summary>
public sealed class DiscrepancyService : IDiscrepancyService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public DiscrepancyService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<DiscrepancySummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Discrepancy? discrepancy = await _context.Discrepancies.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        return discrepancy is null ? null : ToSummary(discrepancy);
    }

    public async Task<KeysetPage<DiscrepancySummary>> ListAsync(
        int limit, string? cursor, IReadOnlySet<Guid>? warehouseIds, IReadOnlySet<Guid>? restaurantIds, DiscrepancyType? type, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Discrepancy> query = _context.Discrepancies.AsNoTracking();

        if (warehouseIds is not null && restaurantIds is not null)
        {
            query = query.Where(d =>
                (d.WarehouseId != null && warehouseIds.Contains(d.WarehouseId.Value)) ||
                (d.RestaurantId != null && restaurantIds.Contains(d.RestaurantId.Value)));
        }
        else if (warehouseIds is not null)
        {
            query = query.Where(d => d.WarehouseId != null && warehouseIds.Contains(d.WarehouseId.Value));
        }
        else if (restaurantIds is not null)
        {
            query = query.Where(d => d.RestaurantId != null && restaurantIds.Contains(d.RestaurantId.Value));
        }

        if (type is not null)
        {
            query = query.Where(d => d.Type == type.Value);
        }

        query = query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Discrepancy> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Discrepancy> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<DiscrepancySummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<TransactionalResult<DiscrepancySummary>> ResolveAsync(Guid id, ResolveDiscrepancyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Discrepancy? discrepancy = await _context.Discrepancies.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (discrepancy is null)
        {
            return TransactionalResult.Failure<DiscrepancySummary>(TransactionalError.NotFound);
        }

        try
        {
            // Task 12.5 (ADR-021): resolution ends here - no IStockPostingService call exists
            // anywhere in this method, deliberately. Stock reconciliation happens only through a
            // physical stock count (Phase 13).
            discrepancy.Resolve(_currentUserService.UserId, command.Reason);
        }
        catch (InvalidOperationException)
        {
            return TransactionalResult.Failure<DiscrepancySummary>(TransactionalError.InvalidStateTransition);
        }
        catch (ArgumentException)
        {
            return TransactionalResult.Failure<DiscrepancySummary>(TransactionalError.InvalidStateTransition);
        }

        _auditLogger.Record(new AuditEntry(
            "DISCREPANCY_RESOLVED", nameof(Discrepancy), discrepancy.Id,
            $"تم حل الفرق: {discrepancy.DocumentNumber} - السبب: {command.Reason}",
            OldValues: null, NewValues: new { command.Reason }, Domain.Enums.AuditResult.Success,
            WarehouseId: discrepancy.WarehouseId, RestaurantId: discrepancy.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(ToSummary(discrepancy));
    }

    private static DiscrepancySummary ToSummary(Discrepancy discrepancy) => new(
        discrepancy.Id, discrepancy.DocumentNumber, discrepancy.Type, discrepancy.ReferenceType, discrepancy.ReferenceId,
        discrepancy.ReferenceLineId, discrepancy.WarehouseId, discrepancy.RestaurantId, discrepancy.ItemId,
        discrepancy.ExpectedQuantity, discrepancy.ActualQuantity, discrepancy.Variance, discrepancy.Status,
        discrepancy.Reason, discrepancy.ResolvedBy, discrepancy.ResolvedAt, discrepancy.CreatedAt);
}
