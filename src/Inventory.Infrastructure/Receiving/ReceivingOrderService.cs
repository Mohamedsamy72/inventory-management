using System.Text.Json;
using Inventory.Application.Common;
using Microsoft.AspNetCore.Http;
using Inventory.Application.Receiving;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Receiving;

/// <summary>See <see cref="IReceivingOrderService"/>.</summary>
public sealed class ReceivingOrderService : IReceivingOrderService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IStockPostingService _stockPostingService;
    private readonly IFinancialProjection _financialProjection;
    private readonly IInTransitCalculator _inTransitCalculator;
    private readonly IIdempotencyService _idempotencyService;

    public ReceivingOrderService(
        InventoryDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogger auditLogger,
        IDocumentSequenceService sequenceService,
        IStockPostingService stockPostingService,
        IFinancialProjection financialProjection,
        IInTransitCalculator inTransitCalculator,
        IIdempotencyService idempotencyService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _sequenceService = sequenceService ?? throw new ArgumentNullException(nameof(sequenceService));
        _stockPostingService = stockPostingService ?? throw new ArgumentNullException(nameof(stockPostingService));
        _financialProjection = financialProjection ?? throw new ArgumentNullException(nameof(financialProjection));
        _inTransitCalculator = inTransitCalculator ?? throw new ArgumentNullException(nameof(inTransitCalculator));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
    }

    public async Task<TransactionalResult<ReceivingOrderSummary>> CreateDraftAsync(CreateReceivingOrderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        string documentNumber = await _sequenceService.AllocateAsync(DocumentType.ReceivingOrder, cancellationToken);

        var order = new ReceivingOrder(
            _currentUserService.CompanyId, command.WarehouseId, command.SupplierId, documentNumber,
            command.BusinessDate, _currentUserService.UserId);
        _context.ReceivingOrders.Add(order);

        _auditLogger.Record(new AuditEntry(
            "RECEIVING_ORDER_CREATED", nameof(ReceivingOrder), order.Id,
            $"تم إنشاء أمر استلام جديد: {order.DocumentNumber}",
            OldValues: null, NewValues: new { order.DocumentNumber, order.WarehouseId }, Domain.Enums.AuditResult.Success,
            WarehouseId: order.WarehouseId));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TransactionalResult.Success(await ToSummaryAsync(order, cancellationToken));
    }

    public async Task<ReceivingOrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ReceivingOrder? order = await _context.ReceivingOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return order is null ? null : await ToSummaryAsync(order, cancellationToken);
    }

    public async Task<KeysetPage<ReceivingOrderSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<ReceivingOrder> query = _context.ReceivingOrders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<ReceivingOrder> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<ReceivingOrder> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        var items = new List<ReceivingOrderSummary>();
        foreach (ReceivingOrder order in page.Items)
        {
            items.Add(await ToSummaryAsync(order, cancellationToken));
        }

        return new KeysetPage<ReceivingOrderSummary>(items, page.NextCursor);
    }

    public async Task<TransactionalResult<ReceivingOrderLineSummary>> AddLineAsync(
        Guid orderId, AddReceivingOrderLineCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ReceivingOrder? order = await _context.ReceivingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return TransactionalResult.Failure<ReceivingOrderLineSummary>(TransactionalError.NotFound);
        }

        if (order.Status != ReceivingOrderStatus.Draft)
        {
            return TransactionalResult.Failure<ReceivingOrderLineSummary>(TransactionalError.InvalidStateTransition);
        }

        Item? item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == command.ItemId, cancellationToken);
        if (item is null)
        {
            return TransactionalResult.Failure<ReceivingOrderLineSummary>(TransactionalError.NotFound);
        }

        decimal baseQuantity;
        if (command.UnitId == item.BaseUnitId)
        {
            baseQuantity = command.ExpectedQuantity;
        }
        else
        {
            ItemUnitConversion? conversion = await _context.ItemUnitConversions
                .Where(c => c.ItemId == command.ItemId && c.FromUnitId == command.UnitId && c.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (conversion is null)
            {
                return TransactionalResult.Failure<ReceivingOrderLineSummary>(TransactionalError.InvalidStateTransition, "CONVERSION_NOT_DEFINED");
            }

            baseQuantity = command.ExpectedQuantity * conversion.ConversionFactor;
        }

        var line = new ReceivingOrderItem(orderId, command.ItemId, command.ExpectedQuantity, command.UnitId, baseQuantity, command.Notes);
        line.SetPostedCost(command.UnitCost, command.ExpectedQuantity * command.UnitCost);
        _context.ReceivingOrderItems.Add(line);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // uq_roi_item (CR-023): a second line for the same item on this document.
            return TransactionalResult.Failure<ReceivingOrderLineSummary>(TransactionalError.InvalidStateTransition, "DUPLICATE_LINE_ITEM");
        }

        return TransactionalResult.Success(ToLineSummary(line));
    }

    public async Task<TransactionalResult<bool>> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken cancellationToken)
    {
        ReceivingOrder? order = await _context.ReceivingOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return TransactionalResult.Failure<bool>(TransactionalError.NotFound);
        }

        if (order.Status != ReceivingOrderStatus.Draft)
        {
            return TransactionalResult.Failure<bool>(TransactionalError.InvalidStateTransition);
        }

        ReceivingOrderItem? line = await _context.ReceivingOrderItems.FirstOrDefaultAsync(l => l.Id == lineId && l.ReceivingOrderId == orderId, cancellationToken);
        if (line is null)
        {
            return TransactionalResult.Failure<bool>(TransactionalError.NotFound);
        }

        _context.ReceivingOrderItems.Remove(line);
        await _context.SaveChangesAsync(cancellationToken);
        return TransactionalResult.Success(true);
    }

    public async Task<TransactionalResult<ReceivingOrderSummary>> SubmitAsync(Guid orderId, IdempotencyContext idempotency, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(idempotency);

        ReceivingOrder? order = await _context.ReceivingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.NotFound);
        }

        if (order.Status != ReceivingOrderStatus.Draft)
        {
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.InvalidStateTransition);
        }

        List<ReceivingOrderItem> lines = await _context.ReceivingOrderItems
            .Where(l => l.ReceivingOrderId == orderId).ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.EmptyDocument);
        }

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // Task 5.4/T1 (docs/30 §4): INCOMING_POSTED per line, at the expected quantity - what
        // was actually delivered is unknown until verification.
        var postingLines = lines
            .Select(l => new StockPostingLine(
                order.WarehouseId, l.ItemId, l.UnitId, l.ExpectedQuantity, l.BaseQuantity,
                MovementType.IncomingPosted, ReferenceType.ReceivingOrder, order.Id, l.UnitCost))
            .ToList();

        try
        {
            await _stockPostingService.PostAsync(postingLines, cancellationToken);
        }
        catch (InsufficientStockException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.InsufficientStock);
        }

        order.Submit();

        _auditLogger.Record(new AuditEntry(
            "RECEIVING_ORDER_SUBMITTED", nameof(ReceivingOrder), order.Id,
            $"تم ترحيل أمر الاستلام: {order.DocumentNumber}",
            OldValues: null, NewValues: new { LineCount = lines.Count }, Domain.Enums.AuditResult.Success,
            WarehouseId: order.WarehouseId));

        ReceivingOrderSummary summary = BuildSummary(order, lines);
        return await SaveWithIdempotencyAsync(transaction, idempotency, summary, cancellationToken);
    }

    public async Task<TransactionalResult<ReceivingOrderSummary>> VerifyAsync(
        Guid orderId, VerifyReceivingOrderCommand command, IdempotencyContext idempotency, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(idempotency);

        ReceivingOrder? order = await _context.ReceivingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.NotFound);
        }

        if (order.Status != ReceivingOrderStatus.Submitted)
        {
            // Covers CR-041's double-verify case too: a second verify finds the order already
            // Verified, which is not Submitted, so it fails here before touching any line.
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.InvalidStateTransition);
        }

        var linesById = await _context.ReceivingOrderItems
            .Where(l => l.ReceivingOrderId == orderId)
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var postingLines = new List<StockPostingLine>();
        foreach (VerifyReceivingOrderLineCommand lineCommand in command.Lines)
        {
            if (!linesById.TryGetValue(lineCommand.LineId, out ReceivingOrderItem? line))
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.NotFound);
            }

            // line.BaseQuantity was resolved against ExpectedQuantity at add time (constant
            // conversion factor for a fixed unit), so the same ratio applies to the actual.
            decimal actualBaseQuantity = lineCommand.ActualQuantity * (line.BaseQuantity / line.ExpectedQuantity);

            // docs/09 §Phase 8 "Risks" - the delta, never the full actual, is what gets posted.
            decimal deltaBaseQuantity = actualBaseQuantity - line.BaseQuantity;

            line.RecordReconciliation(lineCommand.ActualQuantity, actualBaseQuantity);

            if (deltaBaseQuantity != 0)
            {
                postingLines.Add(new StockPostingLine(
                    order.WarehouseId, line.ItemId, line.UnitId, lineCommand.ActualQuantity - line.ExpectedQuantity, deltaBaseQuantity,
                    MovementType.IncomingReconciliation, ReferenceType.ReceivingOrder, order.Id, line.UnitCost));
            }
        }

        try
        {
            if (postingLines.Count > 0)
            {
                await _stockPostingService.PostAsync(postingLines, cancellationToken);
            }
        }
        catch (InsufficientStockException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.InsufficientStock);
        }

        order.Verify(_currentUserService.UserId);

        _auditLogger.Record(new AuditEntry(
            "RECEIVING_ORDER_VERIFIED", nameof(ReceivingOrder), order.Id,
            $"تم مطابقة أمر الاستلام: {order.DocumentNumber}",
            OldValues: null, NewValues: new { AdjustedLines = postingLines.Count }, Domain.Enums.AuditResult.Success,
            WarehouseId: order.WarehouseId));

        ReceivingOrderSummary summary = BuildSummary(order, linesById.Values);
        return await SaveWithIdempotencyAsync(transaction, idempotency, summary, cancellationToken);
    }

    public async Task<TransactionalResult<ReceivingOrderSummary>> ReverseAsync(Guid orderId, string reason, IdempotencyContext idempotency, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(idempotency);

        ReceivingOrder? order = await _context.ReceivingOrders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.NotFound);
        }

        if (order.Status is not (ReceivingOrderStatus.Submitted or ReceivingOrderStatus.Verified))
        {
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.InvalidStateTransition);
        }

        List<ReceivingOrderItem> lines = await _context.ReceivingOrderItems
            .Where(l => l.ReceivingOrderId == orderId).ToListAsync(cancellationToken);

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        // The net quantity currently on the balance for each line is what verification left it
        // at (ActualBaseQuantity) if reconciled, or the originally posted amount otherwise -
        // reversing means driving that back to zero, valued at the line's own cost (ADR-020
        // treats a correction to a specific posting the same way regardless of which act
        // triggered it).
        var postingLines = lines
            .Select(l =>
            {
                decimal currentlyPosted = l.Reconciled ? l.ActualBaseQuantity!.Value : l.BaseQuantity;
                return new StockPostingLine(
                    order.WarehouseId, l.ItemId, l.UnitId, -(l.Reconciled ? l.ActualQuantity!.Value : l.ExpectedQuantity), -currentlyPosted,
                    MovementType.IncomingReconciliation, ReferenceType.ReceivingOrder, order.Id, l.UnitCost);
            })
            .Where(pl => pl.BaseQuantity != 0)
            .ToList();

        try
        {
            if (postingLines.Count > 0)
            {
                await _stockPostingService.PostAsync(postingLines, cancellationToken);
            }
        }
        catch (InsufficientStockException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.InsufficientStock);
        }

        order.Reverse(_currentUserService.UserId, reason);

        _auditLogger.Record(new AuditEntry(
            "RECEIVING_ORDER_REVERSED", nameof(ReceivingOrder), order.Id,
            $"تم عكس أمر الاستلام: {order.DocumentNumber} - السبب: {reason}",
            OldValues: null, NewValues: new { reason }, Domain.Enums.AuditResult.Success,
            WarehouseId: order.WarehouseId));

        ReceivingOrderSummary summary = BuildSummary(order, lines);
        return await SaveWithIdempotencyAsync(transaction, idempotency, summary, cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseStockLine>> GetWarehouseStockAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        List<StockBalance> balances = await _context.StockBalances.AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId).ToListAsync(cancellationToken);

        IReadOnlyDictionary<Guid, decimal> inTransitByItem = await _inTransitCalculator.GetInTransitQuantitiesByItemAsync(warehouseId, cancellationToken);

        return balances.Select(b =>
        {
            decimal inTransit = inTransitByItem.GetValueOrDefault(b.ItemId);
            return new WarehouseStockLine(b.ItemId, b.Quantity, inTransit, b.Quantity - inTransit, _financialProjection.Apply(b.AverageUnitCost));
        }).ToList();
    }

    private async Task<ReceivingOrderSummary> ToSummaryAsync(ReceivingOrder order, CancellationToken cancellationToken)
    {
        List<ReceivingOrderItem> lines = await _context.ReceivingOrderItems
            .AsNoTracking().Where(l => l.ReceivingOrderId == order.Id).ToListAsync(cancellationToken);

        return BuildSummary(order, lines);
    }

    /// <summary>Builds the summary from already-loaded, in-memory entities rather than
    /// re-querying - Submit/Verify/Reverse need this BEFORE their transaction commits (both to
    /// cache the idempotency response and to return it), when a fresh query would either miss
    /// the uncommitted change entirely or require an extra round trip for no reason.</summary>
    private ReceivingOrderSummary BuildSummary(ReceivingOrder order, IEnumerable<ReceivingOrderItem> lines) => new(
        order.Id, order.DocumentNumber, order.WarehouseId, order.SupplierId, order.Status, order.BusinessDate,
        order.ReversedBy, order.ReversalReason, lines.Select(ToLineSummary).ToList());

    private ReceivingOrderLineSummary ToLineSummary(ReceivingOrderItem line) => new(
        line.Id, line.ItemId, line.ExpectedQuantity, line.ActualQuantity, line.UnitId,
        _financialProjection.Apply(line.UnitCost), _financialProjection.Apply(line.TotalCost), line.Reconciled);

    /// <summary>Records the idempotency response inside the CURRENT unit of work (docs/30 §6.2
    /// step 6), then saves. On the rare concurrent-duplicate race - two identical requests both
    /// observing <see cref="IdempotencyOutcome.New"/> and racing to insert - the unique
    /// constraint throws here; the caller's transaction is rolled back and this re-checks to
    /// return the winner's now-committed response instead of a spurious error.</summary>
    private async Task<TransactionalResult<ReceivingOrderSummary>> SaveWithIdempotencyAsync(
        IDbContextTransaction transaction, IdempotencyContext idempotency, ReceivingOrderSummary summary, CancellationToken cancellationToken)
    {
        _idempotencyService.RecordResponse(idempotency.Key, idempotency.Endpoint, idempotency.RawRequestBody, StatusCodes.Status200OK, summary);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            IdempotencyCheckResult recheck = await _idempotencyService.CheckAsync(idempotency.Key, idempotency.Endpoint, idempotency.RawRequestBody, cancellationToken);
            if (recheck.Outcome == IdempotencyOutcome.Replay && recheck.CachedPayloadJson is not null)
            {
                ReceivingOrderSummary winner = JsonSerializer.Deserialize<ReceivingOrderSummary>(recheck.CachedPayloadJson)!;
                return TransactionalResult.Success(winner);
            }

            return TransactionalResult.Failure<ReceivingOrderSummary>(TransactionalError.ConcurrencyConflict);
        }

        await transaction.CommitAsync(cancellationToken);
        return TransactionalResult.Success(summary);
    }
}
