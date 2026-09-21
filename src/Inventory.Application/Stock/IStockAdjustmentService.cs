using Inventory.Application.Common;
using Inventory.Application.Receiving;

namespace Inventory.Application.Stock;

/// <summary>
/// Owner-grantable direct stock correction (`stock:direct_set`). The stated quantity becomes the item's
/// balance in the warehouse, expressed in the item's BASE unit. It is never an edit: the difference is posted
/// as a `PhysicalAdjustment` ledger row (reference `ManualAdjustment`) through <see cref="IStockPostingService"/>,
/// so history is preserved and the ledger stays append-only. Only warehouses hold stock - nothing here touches
/// a restaurant. When <c>unitCost</c> is given and the balance goes UP, the increase is posted as an OpeningBalance
/// movement carrying that cost (the weighted-average cost is recomputed); without a cost it is a plain adjustment.
/// The caller must already have checked that the user may enter costs.
/// </summary>
public interface IStockAdjustmentService
{
    Task<TransactionalResult<WarehouseStockLine>> SetStockAsync(
        Guid warehouseId, Guid itemId, decimal newBaseQuantity, decimal? unitCost, string? reason, CancellationToken cancellationToken);

    /// <summary>Removes an item from a warehouse's stock: the whole balance is written off to zero as a
    /// `PhysicalAdjustment` ledger row (history is kept - the ledger is append-only) and an audit entry records who removed
    /// which item and how much was there. Refused while any of the item is in transit from this warehouse. The caller must
    /// already have re-verified the user's password.</summary>
    Task<TransactionalResult<StockRemovalResult>> RemoveItemAsync(Guid warehouseId, Guid itemId, string? reason, CancellationToken cancellationToken);
}

public sealed record StockRemovalResult(Guid ItemId, string ItemName, decimal PreviousQuantity, string UnitName);
