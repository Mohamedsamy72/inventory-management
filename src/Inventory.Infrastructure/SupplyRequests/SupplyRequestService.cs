using Inventory.Application.Common;
using Inventory.Application.SupplyRequests;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.SupplyRequests;

/// <summary>See <see cref="ISupplyRequestService"/>.</summary>
public sealed class SupplyRequestService : ISupplyRequestService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IUnitConversionResolver _unitConversionResolver;

    public SupplyRequestService(
        InventoryDbContext context,
        ICurrentUserService currentUserService,
        IAuditLogger auditLogger,
        IDocumentSequenceService sequenceService,
        IUnitConversionResolver unitConversionResolver)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _sequenceService = sequenceService ?? throw new ArgumentNullException(nameof(sequenceService));
        _unitConversionResolver = unitConversionResolver ?? throw new ArgumentNullException(nameof(unitConversionResolver));
    }

    public async Task<TransactionalResult<SupplyRequestSummary>> CreateDraftAsync(CreateSupplyRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Restaurant? restaurant = await _context.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == command.RestaurantId, cancellationToken);
        if (restaurant is null)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.NotFound);
        }

        // Change 1 (reversed ADR-028): the warehouse is chosen by the client, not derived - but
        // still validated here, never trusted as-is. `_context.Warehouses` already carries the
        // tenant global query filter (ADR-016), so a cross-company id is indistinguishable from
        // a nonexistent one and both correctly fail closed with the same error.
        Warehouse? warehouse = await _context.Warehouses.AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == command.WarehouseId, cancellationToken);
        if (warehouse is null || warehouse.Status != WarehouseStatus.Active)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.WarehouseUnavailable);
        }

        // Product decision: the restaurant may only request from warehouses Owner/Admin explicitly
        // allowed for it (restaurant_warehouses) - same fail-closed error as an unknown/inactive one.
        bool allowed = await _context.RestaurantWarehouses.AsNoTracking()
            .AnyAsync(m => m.RestaurantId == command.RestaurantId && m.WarehouseId == command.WarehouseId, cancellationToken);
        if (!allowed)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.WarehouseUnavailable);
        }

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        string documentNumber = await _sequenceService.AllocateAsync(DocumentType.SupplyRequest, cancellationToken);

        var request = new SupplyRequest(_currentUserService.CompanyId, command.RestaurantId, warehouse.Id, documentNumber, _currentUserService.UserId);
        _context.SupplyRequests.Add(request);

        _auditLogger.Record(new AuditEntry(
            "SUPPLY_REQUEST_CREATED", nameof(SupplyRequest), request.Id,
            $"تم إنشاء طلب توريد جديد: {request.DocumentNumber}",
            OldValues: null, NewValues: new { request.DocumentNumber, request.RestaurantId, request.WarehouseId }, Domain.Enums.AuditResult.Success,
            RestaurantId: request.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return TransactionalResult.Success(BuildSummary(request, []));
    }

    public async Task<SupplyRequestSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        SupplyRequest? request = await _context.SupplyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return request is null ? null : await ToSummaryAsync(request, cancellationToken);
    }

    public async Task<KeysetPage<SupplyRequestSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<SupplyRequest> query = _context.SupplyRequests.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<SupplyRequest> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<SupplyRequest> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        var items = new List<SupplyRequestSummary>();
        foreach (SupplyRequest request in page.Items)
        {
            items.Add(await ToSummaryAsync(request, cancellationToken));
        }

        return new KeysetPage<SupplyRequestSummary>(items, page.NextCursor);
    }

    public async Task<TransactionalResult<SupplyRequestLineSummary>> AddLineAsync(
        Guid requestId, AddSupplyRequestItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        SupplyRequest? request = await _context.SupplyRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.NotFound);
        }

        if (request.Status != SupplyRequestStatus.Draft)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.InvalidStateTransition);
        }

        bool itemExists = await _context.Items.AsNoTracking().AnyAsync(i => i.Id == command.ItemId, cancellationToken);
        if (!itemExists)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.NotFound);
        }

        UnitConversionResolution resolution = await _unitConversionResolver.ResolveAsync(command.ItemId, command.UnitId, command.RequestedQuantity, cancellationToken);
        if (!resolution.Succeeded)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.InvalidStateTransition, ErrorCodeConversionNotDefined);
        }

        SupplyRequestItem? existing = await _context.SupplyRequestItems
            .FirstOrDefaultAsync(l => l.SupplyRequestId == requestId && l.ItemId == command.ItemId, cancellationToken);

        if (existing is not null)
        {
            // Task 9.4: merge into the existing line rather than reject - but only when the unit
            // matches, since summing RequestedQuantity across two different display units would
            // silently corrupt the total (BaseQuantity alone isn't enough to disambiguate what
            // the line's own displayed quantity/unit should read afterward).
            if (existing.UnitId != command.UnitId)
            {
                // No dedicated docs/13 catalogue code for this case - the generic
                // INVALID_STATE_TRANSITION code (TransactionalErrorWriter's default) is the
                // correct response, not an invented one.
                return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.InvalidStateTransition);
            }

            existing.MergeAdditionalQuantity(command.RequestedQuantity, resolution.BaseQuantity, command.Notes);
            await _context.SaveChangesAsync(cancellationToken);
            return TransactionalResult.Success(ToLineSummary(existing));
        }

        var line = new SupplyRequestItem(requestId, command.ItemId, command.RequestedQuantity, command.UnitId, resolution.BaseQuantity, command.Notes);
        _context.SupplyRequestItems.Add(line);
        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(ToLineSummary(line));
    }

    public async Task<TransactionalResult<SupplyRequestLineSummary>> UpdateLineAsync(
        Guid requestId, Guid lineId, UpdateSupplyRequestItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        SupplyRequest? request = await _context.SupplyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.NotFound);
        }

        if (request.Status != SupplyRequestStatus.Draft)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.InvalidStateTransition);
        }

        SupplyRequestItem? line = await _context.SupplyRequestItems.FirstOrDefaultAsync(l => l.Id == lineId && l.SupplyRequestId == requestId, cancellationToken);
        if (line is null)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.NotFound);
        }

        UnitConversionResolution resolution = await _unitConversionResolver.ResolveAsync(line.ItemId, command.UnitId, command.RequestedQuantity, cancellationToken);
        if (!resolution.Succeeded)
        {
            return TransactionalResult.Failure<SupplyRequestLineSummary>(TransactionalError.InvalidStateTransition, ErrorCodeConversionNotDefined);
        }

        line.UpdateRequest(command.RequestedQuantity, command.UnitId, resolution.BaseQuantity, command.Notes);
        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(ToLineSummary(line));
    }

    public async Task<TransactionalResult<bool>> RemoveLineAsync(Guid requestId, Guid lineId, CancellationToken cancellationToken)
    {
        SupplyRequest? request = await _context.SupplyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return TransactionalResult.Failure<bool>(TransactionalError.NotFound);
        }

        if (request.Status != SupplyRequestStatus.Draft)
        {
            return TransactionalResult.Failure<bool>(TransactionalError.InvalidStateTransition);
        }

        SupplyRequestItem? line = await _context.SupplyRequestItems.FirstOrDefaultAsync(l => l.Id == lineId && l.SupplyRequestId == requestId, cancellationToken);
        if (line is null)
        {
            return TransactionalResult.Failure<bool>(TransactionalError.NotFound);
        }

        _context.SupplyRequestItems.Remove(line);
        await _context.SaveChangesAsync(cancellationToken);
        return TransactionalResult.Success(true);
    }

    public async Task<TransactionalResult<SupplyRequestSummary>> SubmitAsync(Guid requestId, CancellationToken cancellationToken)
    {
        SupplyRequest? request = await _context.SupplyRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.NotFound);
        }

        if (request.Status != SupplyRequestStatus.Draft)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.InvalidStateTransition);
        }

        List<SupplyRequestItem> lines = await _context.SupplyRequestItems
            .Where(l => l.SupplyRequestId == requestId).ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.EmptyDocument);
        }

        List<Guid> itemIds = lines.Select(l => l.ItemId).ToList();
        bool allActive = await _context.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id)).AllAsync(i => i.IsActive, cancellationToken);
        if (!allActive)
        {
            // No dedicated docs/13 catalogue code for an inactive item at submit time - generic
            // INVALID_STATE_TRANSITION.
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.InvalidStateTransition);
        }

        // Task 9.6/9.11: submitting a supply request has ZERO stock effect - no
        // IStockPostingService call belongs anywhere in this method.
        request.Submit();

        _auditLogger.Record(new AuditEntry(
            "SUPPLY_REQUEST_SUBMITTED", nameof(SupplyRequest), request.Id,
            $"تم إرسال طلب التوريد: {request.DocumentNumber}",
            OldValues: null, NewValues: new { LineCount = lines.Count }, Domain.Enums.AuditResult.Success,
            RestaurantId: request.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(BuildSummary(request, lines));
    }

    public async Task<TransactionalResult<SupplyRequestSummary>> CancelAsync(Guid requestId, CancellationToken cancellationToken)
    {
        SupplyRequest? request = await _context.SupplyRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.NotFound);
        }

        try
        {
            request.Cancel();
        }
        catch (InvalidOperationException)
        {
            return TransactionalResult.Failure<SupplyRequestSummary>(TransactionalError.InvalidStateTransition);
        }

        _auditLogger.Record(new AuditEntry(
            "SUPPLY_REQUEST_CANCELLED", nameof(SupplyRequest), request.Id,
            $"تم إلغاء طلب التوريد: {request.DocumentNumber}",
            OldValues: null, NewValues: null, Domain.Enums.AuditResult.Success,
            RestaurantId: request.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);

        return TransactionalResult.Success(await ToSummaryAsync(request, cancellationToken));
    }

    private async Task<SupplyRequestSummary> ToSummaryAsync(SupplyRequest request, CancellationToken cancellationToken)
    {
        List<SupplyRequestItem> lines = await _context.SupplyRequestItems
            .AsNoTracking().Where(l => l.SupplyRequestId == request.Id).ToListAsync(cancellationToken);

        return BuildSummary(request, lines);
    }

    private static SupplyRequestSummary BuildSummary(SupplyRequest request, IEnumerable<SupplyRequestItem> lines) => new(
        request.Id, request.DocumentNumber, request.RestaurantId, request.WarehouseId, request.Status,
        request.RequestedBy, request.RequestedAt, lines.Select(ToLineSummary).ToList());

    private static SupplyRequestLineSummary ToLineSummary(SupplyRequestItem line) => new(
        line.Id, line.ItemId, line.RequestedQuantity, line.FulfilledQuantity, line.UnitId, line.Notes);

    /// <summary>docs/13's real catalogue code for this case (Phase 6) - matches
    /// <c>ErrorCodes.ConversionNotDefined</c> in the Api layer, referenced by string here since
    /// Application cannot depend on Api.</summary>
    private const string ErrorCodeConversionNotDefined = "CONVERSION_NOT_DEFINED";
}
