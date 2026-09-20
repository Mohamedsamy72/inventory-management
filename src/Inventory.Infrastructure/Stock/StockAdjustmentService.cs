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

    public StockAdjustmentService(
        InventoryDbContext context, IStockPostingService stockPostingService, IInTransitCalculator inTransitCalculator,
        IFinancialProjection financialProjection, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _stockPostingService = stockPostingService ?? throw new ArgumentNullException(nameof(stockPostingService));
        _inTransitCalculator = inTransitCalculator ?? throw new ArgumentNullException(nameof(inTransitCalculator));
        _financialProjection = financialProjection ?? throw new ArgumentNullException(nameof(financialProjection));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<TransactionalResult<WarehouseStockLine>> SetStockAsync(
        Guid warehouseId, Guid itemId, decimal newBaseQuantity, string? reason, CancellationToken cancellationToken)
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

        if (delta != 0)
        {
            try
            {
                await _stockPostingService.PostAsync(
                [
                    new StockPostingLine(
                        warehouseId, itemId, item.BaseUnitId, delta, delta,
                        MovementType.PhysicalAdjustment, ReferenceType.ManualAdjustment, Guid.NewGuid(), UnitCost: null),
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
                OldValues: new { Quantity = current }, NewValues: new { Quantity = newBaseQuantity, Reason = reason },
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
}
