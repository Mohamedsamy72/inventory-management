using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Application.StockCounts;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.StockCounts;

/// <summary>See <see cref="IStockCountService"/>.</summary>
public sealed class StockCountService : IStockCountService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IInTransitCalculator _inTransitCalculator;
    private readonly IStockPostingService _stockPostingService;
    private readonly IIdempotencyService _idempotencyService;

    public StockCountService(
        InventoryDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogger auditLogger,
        IDocumentSequenceService sequenceService,
        IInTransitCalculator inTransitCalculator,
        IStockPostingService stockPostingService,
        IIdempotencyService idempotencyService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _sequenceService = sequenceService ?? throw new ArgumentNullException(nameof(sequenceService));
        _inTransitCalculator = inTransitCalculator ?? throw new ArgumentNullException(nameof(inTransitCalculator));
        _stockPostingService = stockPostingService ?? throw new ArgumentNullException(nameof(stockPostingService));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
    }

    public async Task<TransactionalResult<StockCountSummary>> CreateAsync(CreateStockCountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        List<StockBalance> balances = await _context.StockBalances.AsNoTracking()
            .Where(b => b.WarehouseId == command.WarehouseId).ToListAsync(cancellationToken);

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        string documentNumber = await _sequenceService.AllocateAsync(DocumentType.StockCount, cancellationToken);
        var stockCount = new StockCount(_currentUserService.CompanyId, command.WarehouseId, documentNumber, command.IsBlindCount, _currentUserService.UserId);
        stockCount.BeginCounting();
        _context.StockCounts.Add(stockCount);

        var lines = new List<StockCountItem>();
        foreach (StockBalance balance in balances)
        {
            // Task 13.3/ADR-018: the snapshotted expected figure excludes in-transit goods -
            // this is the entire reason Phase 13 follows Phase 10, not merely raw balance.
            decimal inTransit = await _inTransitCalculator.GetInTransitQuantityAsync(command.WarehouseId, balance.ItemId, cancellationToken);
            var line = new StockCountItem(stockCount.Id, balance.ItemId, balance.Quantity - inTransit, balance.BaseUnitId, notes: null);
            lines.Add(line);
            _context.StockCountItems.Add(line);
        }

        _auditLogger.Record(new AuditEntry(
            "STOCK_COUNT_OPENED", nameof(StockCount), stockCount.Id,
            $"تم بدء جرد جديد: {stockCount.DocumentNumber}",
            OldValues: null, NewValues: new { stockCount.DocumentNumber, stockCount.IsBlindCount, LineCount = lines.Count }, Domain.Enums.AuditResult.Success,
            WarehouseId: stockCount.WarehouseId));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TransactionalResult.Success(BuildSummary(stockCount, lines));
    }

    public async Task<StockCountSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        StockCount? stockCount = await _context.StockCounts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return stockCount is null ? null : await ToSummaryAsync(stockCount, cancellationToken);
    }

    public async Task<KeysetPage<StockCountSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<StockCount> query = _context.StockCounts.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<StockCount> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<StockCount> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        var items = new List<StockCountSummary>();
        foreach (StockCount stockCount in page.Items)
        {
            items.Add(await ToSummaryAsync(stockCount, cancellationToken));
        }

        return new KeysetPage<StockCountSummary>(items, page.NextCursor);
    }

    public async Task<TransactionalResult<StockCountSummary>> RecordAsync(Guid id, RecordStockCountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        StockCount? stockCount = await _context.StockCounts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (stockCount is null)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.NotFound);
        }

        if (stockCount.Status != StockCountStatus.InProgress)
        {
            // Task 13.10: once Approved (or PendingApproval/Rejected), lines are immutable -
            // this single status check is the entire enforcement, since RecordAsync is the only
            // write path to a line's physical quantity.
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.InvalidStateTransition);
        }

        Dictionary<Guid, StockCountItem> linesById = await _context.StockCountItems
            .Where(l => l.StockCountId == id).ToDictionaryAsync(l => l.Id, cancellationToken);

        foreach (RecordStockCountLineCommand lineCommand in command.Lines)
        {
            if (!linesById.TryGetValue(lineCommand.LineId, out StockCountItem? line))
            {
                return TransactionalResult.Failure<StockCountSummary>(TransactionalError.NotFound);
            }

            line.RecordPhysicalCount(lineCommand.PhysicalQuantity);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Deliberately NOT revealing system quantities here even though the caller just entered
        // these exact physical figures themselves - doing so would immediately tell a blind
        // counter what the system expected for the lines they just submitted, letting them
        // adjust later lines in the same count accordingly and defeating the whole control.
        return TransactionalResult.Success(BuildSummary(stockCount, linesById.Values));
    }

    public async Task<TransactionalResult<StockCountSummary>> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken)
    {
        StockCount? stockCount = await _context.StockCounts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (stockCount is null)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.NotFound);
        }

        try
        {
            stockCount.SubmitForApproval();
        }
        catch (InvalidOperationException)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.InvalidStateTransition);
        }

        _auditLogger.Record(new AuditEntry(
            "STOCK_COUNT_SUBMITTED", nameof(StockCount), stockCount.Id,
            $"تم إرسال الجرد للاعتماد: {stockCount.DocumentNumber}",
            OldValues: null, NewValues: null, Domain.Enums.AuditResult.Success,
            WarehouseId: stockCount.WarehouseId));

        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(await ToSummaryAsync(stockCount, cancellationToken));
    }

    public async Task<TransactionalResult<StockCountSummary>> ApproveAsync(Guid id, IdempotencyContext idempotency, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(idempotency);

        StockCount? stockCount = await _context.StockCounts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (stockCount is null)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.NotFound);
        }

        if (stockCount.Status != StockCountStatus.PendingApproval)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.InvalidStateTransition);
        }

        List<StockCountItem> lines = await _context.StockCountItems.Where(l => l.StockCountId == id).ToListAsync(cancellationToken);

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var postingLines = new List<StockPostingLine>();
        var discrepancies = new List<Discrepancy>();

        foreach (StockCountItem line in lines)
        {
            decimal variance = line.Variance ?? 0m;
            if (variance == 0)
            {
                continue;
            }

            postingLines.Add(new StockPostingLine(
                stockCount.WarehouseId, line.ItemId, line.BaseUnitId, variance, variance,
                MovementType.PhysicalAdjustment, ReferenceType.StockCount, stockCount.Id, UnitCost: null));

            string discrepancyNumber = await _sequenceService.AllocateAsync(DocumentType.Discrepancy, cancellationToken);
            discrepancies.Add(new Discrepancy(
                stockCount.CompanyId, discrepancyNumber, DiscrepancyType.StockCountVariance, ReferenceType.StockCount, stockCount.Id,
                line.Id, stockCount.WarehouseId, null, line.ItemId, line.SystemQuantity, line.PhysicalQuantity ?? line.SystemQuantity));
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
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.InsufficientStock);
        }

        stockCount.Approve(_currentUserService.UserId);

        foreach (Discrepancy discrepancy in discrepancies)
        {
            _context.Discrepancies.Add(discrepancy);
        }

        _auditLogger.Record(new AuditEntry(
            "STOCK_COUNT_APPROVED", nameof(StockCount), stockCount.Id,
            $"تم اعتماد الجرد: {stockCount.DocumentNumber}",
            OldValues: null, NewValues: new { AdjustedLines = postingLines.Count }, Domain.Enums.AuditResult.Success,
            WarehouseId: stockCount.WarehouseId));

        StockCountSummary summary = BuildSummary(stockCount, lines);

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
                StockCountSummary winner = JsonSerializer.Deserialize<StockCountSummary>(recheck.CachedPayloadJson)!;
                return TransactionalResult.Success(winner);
            }

            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.ConcurrencyConflict);
        }

        await transaction.CommitAsync(cancellationToken);
        return TransactionalResult.Success(summary);
    }

    public async Task<TransactionalResult<StockCountSummary>> RejectAsync(Guid id, CancellationToken cancellationToken)
    {
        StockCount? stockCount = await _context.StockCounts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (stockCount is null)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.NotFound);
        }

        try
        {
            // Task 13.7: zero stock effect - no IStockPostingService call anywhere in this
            // method, deliberately.
            stockCount.Reject();
        }
        catch (InvalidOperationException)
        {
            return TransactionalResult.Failure<StockCountSummary>(TransactionalError.InvalidStateTransition);
        }

        _auditLogger.Record(new AuditEntry(
            "STOCK_COUNT_REJECTED", nameof(StockCount), stockCount.Id,
            $"تم رفض الجرد وإعادته للتسجيل: {stockCount.DocumentNumber}",
            OldValues: null, NewValues: null, Domain.Enums.AuditResult.Success,
            WarehouseId: stockCount.WarehouseId));

        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(await ToSummaryAsync(stockCount, cancellationToken));
    }

    private async Task<StockCountSummary> ToSummaryAsync(StockCount stockCount, CancellationToken cancellationToken)
    {
        List<StockCountItem> lines = await _context.StockCountItems.AsNoTracking().Where(l => l.StockCountId == stockCount.Id).ToListAsync(cancellationToken);
        return BuildSummary(stockCount, lines);
    }

    /// <summary>Task 13.4: system quantity/variance are stripped from the DTO - not merely
    /// omitted client-side - whenever the count is a blind one and still
    /// `Draft`/`InProgress`/`PendingApproval` (i.e. still being counted or awaiting the decision
    /// that reveals it), for EVERY caller including the counter who just submitted the figure
    /// that produced it - revealing it back immediately would let a blind counter learn what the
    /// system expected for the line they just entered and adjust later lines accordingly,
    /// defeating the control within the same count. Once `Approved`/`Rejected`, it is visible to
    /// anyone who can view the count at all.</summary>
    private static StockCountSummary BuildSummary(StockCount stockCount, IEnumerable<StockCountItem> lines)
    {
        bool suppress = stockCount.IsBlindCount
            && stockCount.Status is StockCountStatus.Draft or StockCountStatus.InProgress or StockCountStatus.PendingApproval;

        return new StockCountSummary(
            stockCount.Id, stockCount.DocumentNumber, stockCount.WarehouseId, stockCount.Status, stockCount.IsBlindCount,
            stockCount.OpenedBy, stockCount.OpenedAt, stockCount.ApprovedBy, stockCount.ApprovedAt,
            lines.Select(l => new StockCountLineSummary(
                l.Id, l.ItemId,
                suppress ? null : l.SystemQuantity,
                l.PhysicalQuantity,
                suppress ? null : l.Variance,
                l.BaseUnitId, l.Notes)).ToList());
    }
}
