using Inventory.Application.Common;
using Inventory.Application.Receiving;

namespace Inventory.Application.Stock;

/// <summary>
/// Owner-grantable direct stock correction (`stock:direct_set`). The stated quantity becomes the item's
/// balance in the warehouse, expressed in the item's BASE unit. It is never an edit: the difference is posted
/// as a `PhysicalAdjustment` ledger row (reference `ManualAdjustment`) through <see cref="IStockPostingService"/>,
/// so history is preserved and the ledger stays append-only. Only warehouses hold stock - nothing here touches
/// a restaurant.
/// </summary>
public interface IStockAdjustmentService
{
    Task<TransactionalResult<WarehouseStockLine>> SetStockAsync(
        Guid warehouseId, Guid itemId, decimal newBaseQuantity, string? reason, CancellationToken cancellationToken);
}
