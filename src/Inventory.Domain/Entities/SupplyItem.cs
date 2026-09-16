using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class SupplyItem : Entity
{
    private SupplyItem()
    {
    }

    public SupplyItem(Guid supplyId, Guid? supplyRequestItemId, Guid itemId, decimal dispatchedQuantity, Guid unitId, decimal dispatchedBaseQuantity)
    {
        if (dispatchedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dispatchedQuantity), dispatchedQuantity, "Dispatched quantity must be positive.");
        }

        Id = Guid.NewGuid();
        SupplyId = supplyId;
        SupplyRequestItemId = supplyRequestItemId;
        ItemId = itemId;
        DispatchedQuantity = dispatchedQuantity;
        UnitId = unitId;
        DispatchedBaseQuantity = dispatchedBaseQuantity;
    }

    public Guid SupplyId { get; private set; }

    /// <summary>Links back to the originating request line (DB-07) - without it, request ->
    /// dispatch -> receipt traceability is impossible.</summary>
    public Guid? SupplyRequestItemId { get; private set; }

    public Guid ItemId { get; private set; }
    public decimal DispatchedQuantity { get; private set; }
    public decimal? ReceivedQuantity { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal DispatchedBaseQuantity { get; private set; }
    public decimal? ReceivedBaseQuantity { get; private set; }
    public decimal? Variance { get; private set; }

    public void RecordReceipt(decimal receivedQuantity, decimal receivedBaseQuantity)
    {
        if (receivedQuantity < 0 || receivedQuantity > DispatchedQuantity)
        {
            throw new InvalidOperationException($"SupplyItem {Id}: received quantity {receivedQuantity} must be within [0, {DispatchedQuantity}].");
        }

        ReceivedQuantity = receivedQuantity;
        ReceivedBaseQuantity = receivedBaseQuantity;
        Variance = receivedBaseQuantity - DispatchedBaseQuantity;
    }
}
