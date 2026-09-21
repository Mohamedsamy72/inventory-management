using Inventory.Application.Common;
using Inventory.Application.Receiving;
using Inventory.Application.Stock;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Stock;

/// <summary>See <see cref="IStockAdjustmentService"/>.</summary>
public sealed class StockAdjustmentService : IStockAdjustmentService
{
    private readonly InventoryDbContext _context;
    private readonly IStockPostingService _stockPostingService;
    private readonly IInTransitCalculator _inTransitCalculator;
    private readonly IFinancialProjection _financialProjection;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserService _currentUserService;

    public StockAdjustmentService(
        InventoryDbContext context, IStockPostingService stockPostingService, IInTransitCalculator inTransitCalculator,
        IFinancialProjection financialProjection, IAuditLogger auditLogger, ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stockPostingService = stockPostingService ?? throw new ArgumentNullException(nameof(stockPostingService));
        _inTransitCalculator = inTransitCalculator ?? throw new ArgumentNullException(nameof(inTransitCalculator));
        _financialProjection = financialProjection ?? throw new ArgumentNullException(nameof(financialProjection));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<TransactionalResult<WarehouseStockLine>> SetStockAsync(
        Guid warehouseId, Guid itemId, decimal newBaseQuantity, decimal? unitCost, string? reason, CancellationToken cancellationToken)
    {
        // The tenant query filters make another company's warehouse/item indistinguishable from a missing one.
        Warehouse? warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
        Item? item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (warehouse is null || item is null)
        {
            return TransactionalResult.Failure<WarehouseStockLine>(TransactionalError.NotFound);
        }

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        decimal current = await _context.StockBalances.AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId && b.ItemId == itemId)
            .Select(b => b.Quantity).FirstOrDefaultAsync(cancellationToken);
        decimal delta = newBaseQuantity - current;

        // A cost only means something for an increase: it is the cost of the units being added.
        bool withCost = unitCost is not null && delta > 0;

        if (delta != 0)
        {
            try
            {
                await _stockPostingService.PostAsync(
                [
                    new StockPostingLine(
                        warehouseId, itemId, item.BaseUnitId, delta, delta,
                        withCost ? MovementType.OpeningBalance : MovementType.PhysicalAdjustment, ReferenceType.ManualAdjustment, Guid.NewGuid(), withCost ? unitCost : null),
                ], cancellationToken);
            }
            catch (InsufficientStockException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionalResult.Failure<WarehouseStockLine>(TransactionalError.InsufficientStock);
            }

            _auditLogger.Record(new AuditEntry(
                "STOCK_DIRECTLY_SET", nameof(StockBalance), itemId,
                $"تم تعديل رصيد الصنف مباشرة: {item.NameArabic} في {warehouse.NameArabic} من {current} إلى {newBaseQuantity}",
                OldValues: new { Quantity = current }, NewValues: new { Quantity = newBaseQuantity, UnitCost = withCost ? unitCost : null, Reason = reason },
                Domain.Enums.AuditResult.Success, WarehouseId: warehouseId));
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        StockBalance? balance = await _context.StockBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId, cancellationToken);
        IReadOnlyDictionary<Guid, decimal> inTransit = await _inTransitCalculator.GetInTransitQuantitiesByItemAsync(warehouseId, cancellationToken);
        decimal quantity = balance?.Quantity ?? 0m;
        decimal transit = inTransit.GetValueOrDefault(itemId);
        return TransactionalResult.Success(new WarehouseStockLine(
            itemId, quantity, transit, quantity - transit, _financialProjection.Apply(balance?.AverageUnitCost)));
    }

    public async Task<TransactionalResult<StockRemovalResult>> RemoveItemAsync(Guid warehouseId, Guid itemId, string? reason, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken);
        Item? item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);
        if (warehouse is null || item is null)
        {
            return TransactionalResult.Failure<StockRemovalResult>(TransactionalError.NotFound);
        }

        string unitName = await _context.Units.AsNoTracking().Where(u => u.Id == item.BaseUnitId).Select(u => u.NameArabic).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        decimal current = await _context.StockBalances.AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId && b.ItemId == itemId)
            .Select(b => b.Quantity).FirstOrDefaultAsync(cancellationToken);

        IReadOnlyDictionary<Guid, decimal> inTransit = await _inTransitCalculator.GetInTransitQuantitiesByItemAsync(warehouseId, cancellationToken);
        if (inTransit.GetValueOrDefault(itemId) > 0)
        {
            // A dispatched-but-unconfirmed shipment still expects this stock; removing it would make its confirmation fail.
            await transaction.RollbackAsync(cancellationToken);
            return TransactionalResult.Failure<StockRemovalResult>(TransactionalError.InvalidStateTransition);
        }

        if (current > 0)
        {
            try
            {
                await _stockPostingService.PostAsync(
                [
                    new StockPostingLine(
                        warehouseId, itemId, item.BaseUnitId, -current, -current,
                        MovementType.PhysicalAdjustment, ReferenceType.ManualAdjustment, Guid.NewGuid(), UnitCost: null),
                ], cancellationToken);
            }
            catch (InsufficientStockException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionalResult.Failure<StockRemovalResult>(TransactionalError.ConcurrencyConflict);
            }

            Guid actorId = _currentUserService.UserId;
            string actorName = await _context.Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            _auditLogger.Record(new AuditEntry(
                "STOCK_ITEM_REMOVED", nameof(StockBalance), itemId,
                $"قام المستخدم {actorName} بحذف الصنف {item.NameArabic} من مخزن {warehouse.NameArabic} - الرصيد الذي كان موجوداً: {current} {unitName}",
                OldValues: new { Quantity = current, Unit = unitName, Item = item.NameArabic },
                NewValues: new { Quantity = 0m, Reason = reason },
                Domain.Enums.AuditResult.Success, WarehouseId: warehouseId));
            await _context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TransactionalResult.Success(new StockRemovalResult(itemId, item.NameArabic, current, unitName));
    }
}
