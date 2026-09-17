using Inventory.Application.Common;
using Inventory.Application.Supplies;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Supplies;

/// <summary>See <see cref="ISupplyService"/>.</summary>
public sealed class SupplyService : ISupplyService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IInTransitCalculator _inTransitCalculator;

    public SupplyService(
        InventoryDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogger auditLogger,
        IDocumentSequenceService sequenceService,
        IInTransitCalculator inTransitCalculator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _sequenceService = sequenceService ?? throw new ArgumentNullException(nameof(sequenceService));
        _inTransitCalculator = inTransitCalculator ?? throw new ArgumentNullException(nameof(inTransitCalculator));
    }

    public async Task<TransactionalResult<SupplyOperationResult>> FulfillAsync(
        Guid supplyRequestId, FulfillSupplyRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        SupplyRequest? request = await _context.SupplyRequests.FirstOrDefaultAsync(r => r.Id == supplyRequestId, cancellationToken);
        if (request is null)
        {
            return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.NotFound);
        }

        if (request.Status is not (SupplyRequestStatus.Submitted or SupplyRequestStatus.PartiallyFulfilled))
        {
            return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.InvalidStateTransition);
        }

        if (command.Lines.Count == 0)
        {
            return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.EmptyDocument);
        }

        Dictionary<Guid, SupplyRequestItem> linesById = await _context.SupplyRequestItems
            .Where(l => l.SupplyRequestId == supplyRequestId)
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        string documentNumber = await _sequenceService.AllocateAsync(DocumentType.Supply, cancellationToken);
        var supply = new Supply(request.CompanyId, request.WarehouseId, request.RestaurantId, request.Id, documentNumber, _currentUserService.UserId);
        _context.Supplies.Add(supply);

        var insufficientStockItemIds = new List<Guid>();

        foreach (FulfillSupplyLineCommand lineCommand in command.Lines)
        {
            if (!linesById.TryGetValue(lineCommand.SupplyRequestItemId, out SupplyRequestItem? line))
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.NotFound);
            }

            decimal remaining = line.RequestedQuantity - line.FulfilledQuantity;
            if (lineCommand.FulfilledQuantity < 0 || lineCommand.FulfilledQuantity > remaining)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.InvalidStateTransition);
            }

            line.RecordFulfillment(lineCommand.FulfilledQuantity);

            if (lineCommand.FulfilledQuantity == 0)
            {
                continue;
            }

            // The line's own conversion factor (constant: its BaseQuantity was resolved against
            // its RequestedQuantity, both in the line's fixed unit) applies to any portion of it.
            decimal fulfilledBaseQuantity = lineCommand.FulfilledQuantity * (line.BaseQuantity / line.RequestedQuantity);

            var supplyItem = new SupplyItem(supply.Id, line.Id, line.ItemId, lineCommand.FulfilledQuantity, line.UnitId, fulfilledBaseQuantity);
            _context.SupplyItems.Add(supplyItem);

            // Task 10.6/ADR-019: advisory only - never blocks, never reserves.
            if (await IsInsufficientAsync(request.WarehouseId, line.ItemId, fulfilledBaseQuantity, cancellationToken))
            {
                insufficientStockItemIds.Add(line.ItemId);
            }
        }

        UpdateRequestFulfillmentStatus(request, linesById.Values);

        _auditLogger.Record(new AuditEntry(
            "SUPPLY_FULFILLED", nameof(Supply), supply.Id,
            $"تم تجهيز توريد: {supply.DocumentNumber}",
            OldValues: null, NewValues: new { supply.DocumentNumber, LineCount = command.Lines.Count }, Domain.Enums.AuditResult.Success,
            WarehouseId: supply.WarehouseId, RestaurantId: supply.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        SupplySummary summary = await ToSummaryAsync(supply, cancellationToken);
        return TransactionalResult.Success(new SupplyOperationResult(summary, insufficientStockItemIds));
    }

    public async Task<SupplySummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Supply? supply = await _context.Supplies.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return supply is null ? null : await ToSummaryAsync(supply, cancellationToken);
    }

    public async Task<KeysetPage<SupplySummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Supply> query = _context.Supplies.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Supply> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Supply> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        var items = new List<SupplySummary>();
        foreach (Supply supply in page.Items)
        {
            items.Add(await ToSummaryAsync(supply, cancellationToken));
        }

        return new KeysetPage<SupplySummary>(items, page.NextCursor);
    }

    public async Task<TransactionalResult<SupplyOperationResult>> DispatchAsync(Guid id, CancellationToken cancellationToken)
    {
        Supply? supply = await _context.Supplies.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supply is null)
        {
            return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.NotFound);
        }

        List<SupplyItem> lines = await _context.SupplyItems.AsNoTracking().Where(l => l.SupplyId == id).ToListAsync(cancellationToken);

        try
        {
            supply.Dispatch(_currentUserService.UserId);
        }
        catch (InvalidOperationException)
        {
            return TransactionalResult.Failure<SupplyOperationResult>(TransactionalError.InvalidStateTransition);
        }

        var insufficientStockItemIds = new List<Guid>();
        foreach (SupplyItem line in lines)
        {
            if (await IsInsufficientAsync(supply.WarehouseId, line.ItemId, line.DispatchedBaseQuantity, cancellationToken))
            {
                insufficientStockItemIds.Add(line.ItemId);
            }
        }

        _auditLogger.Record(new AuditEntry(
            "SUPPLY_DISPATCHED", nameof(Supply), supply.Id,
            $"تم إرسال شحنة التوريد: {supply.DocumentNumber}",
            OldValues: null, NewValues: null, Domain.Enums.AuditResult.Success,
            WarehouseId: supply.WarehouseId, RestaurantId: supply.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);

        SupplySummary summary = await ToSummaryAsync(supply, cancellationToken);
        return TransactionalResult.Success(new SupplyOperationResult(summary, insufficientStockItemIds));
    }

    public async Task<TransactionalResult<SupplySummary>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        Supply? supply = await _context.Supplies.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supply is null)
        {
            return TransactionalResult.Failure<SupplySummary>(TransactionalError.NotFound);
        }

        try
        {
            supply.Cancel();
        }
        catch (InvalidOperationException)
        {
            return TransactionalResult.Failure<SupplySummary>(TransactionalError.InvalidStateTransition);
        }

        _auditLogger.Record(new AuditEntry(
            "SUPPLY_CANCELLED", nameof(Supply), supply.Id,
            $"تم إلغاء توريد: {supply.DocumentNumber}",
            OldValues: null, NewValues: null, Domain.Enums.AuditResult.Success,
            WarehouseId: supply.WarehouseId, RestaurantId: supply.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(await ToSummaryAsync(supply, cancellationToken));
    }

    private async Task<bool> IsInsufficientAsync(Guid warehouseId, Guid itemId, decimal baseQuantity, CancellationToken cancellationToken)
    {
        StockBalance? balance = await _context.StockBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId, cancellationToken);
        decimal onHand = balance?.Quantity ?? 0m;
        decimal inTransit = await _inTransitCalculator.GetInTransitQuantityAsync(warehouseId, itemId, cancellationToken);

        return baseQuantity > onHand - inTransit;
    }

    /// <summary>Recomputes the aggregate status from every line's own running total - never from
    /// only the lines touched this round, since a prior round may have already fulfilled some.</summary>
    private static void UpdateRequestFulfillmentStatus(SupplyRequest request, IEnumerable<SupplyRequestItem> allLines)
    {
        List<SupplyRequestItem> lines = allLines.ToList();
        decimal totalRequested = lines.Sum(l => l.RequestedQuantity);
        decimal totalFulfilled = lines.Sum(l => l.FulfilledQuantity);

        if (totalFulfilled <= 0)
        {
            return;
        }

        request.UpdateFulfillmentStatus(totalFulfilled >= totalRequested ? SupplyRequestStatus.Fulfilled : SupplyRequestStatus.PartiallyFulfilled);
    }

    private async Task<SupplySummary> ToSummaryAsync(Supply supply, CancellationToken cancellationToken)
    {
        List<SupplyItem> lines = await _context.SupplyItems.AsNoTracking().Where(l => l.SupplyId == supply.Id).ToListAsync(cancellationToken);
        return BuildSummary(supply, lines);
    }

    private static SupplySummary BuildSummary(Supply supply, IEnumerable<SupplyItem> lines) => new(
        supply.Id, supply.DocumentNumber, supply.WarehouseId, supply.RestaurantId, supply.SupplyRequestId, supply.Status,
        supply.PreparedBy, supply.PreparedAt, supply.DispatchedBy, supply.DispatchedAt,
        lines.Select(l => new SupplyLineSummary(l.Id, l.ItemId, l.DispatchedQuantity, l.ReceivedQuantity, l.UnitId)).ToList());
}
