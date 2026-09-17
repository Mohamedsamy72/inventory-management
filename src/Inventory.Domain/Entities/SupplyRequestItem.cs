using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class SupplyRequestItem : Entity
{
    private SupplyRequestItem()
    {
    }

    public SupplyRequestItem(Guid supplyRequestId, Guid itemId, decimal requestedQuantity, Guid unitId, decimal baseQuantity, string? notes)
    {
        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), requestedQuantity, "Requested quantity must be positive.");
        }

        Id = Guid.NewGuid();
        SupplyRequestId = supplyRequestId;
        ItemId = itemId;
        RequestedQuantity = requestedQuantity;
        FulfilledQuantity = 0m;
        UnitId = unitId;
        BaseQuantity = baseQuantity;
        Notes = notes;
    }

    public Guid SupplyRequestId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal RequestedQuantity { get; private set; }
    public decimal FulfilledQuantity { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public string? Notes { get; private set; }

    public void RecordFulfillment(decimal additionalFulfilledQuantity)
    {
        decimal newTotal = FulfilledQuantity + additionalFulfilledQuantity;
        if (newTotal < 0 || newTotal > RequestedQuantity)
        {
            throw new InvalidOperationException($"SupplyRequestItem {Id}: fulfilled quantity {newTotal} would fall outside [0, {RequestedQuantity}].");
        }

        FulfilledQuantity = newTotal;
    }

    /// <summary>Draft-only line edit (task 9.3) - the service enforces the Draft-only rule, since
    /// this entity has no reference back to its parent's status.</summary>
    public void UpdateRequest(decimal requestedQuantity, Guid unitId, decimal baseQuantity, string? notes)
    {
        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), requestedQuantity, "Requested quantity must be positive.");
        }

        RequestedQuantity = requestedQuantity;
        UnitId = unitId;
        BaseQuantity = baseQuantity;
        Notes = notes;
    }

    /// <summary>Task 9.4 - a second `AddLineAsync` call for an item already on this Draft request
    /// merges into the existing line (adds the quantities) instead of being rejected, since
    /// `uq_sri_item` (CR-023) makes a genuine duplicate row impossible anyway.</summary>
    public void MergeAdditionalQuantity(decimal additionalRequestedQuantity, decimal additionalBaseQuantity, string? notes)
    {
        if (additionalRequestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalRequestedQuantity), additionalRequestedQuantity, "Requested quantity must be positive.");
        }

        RequestedQuantity += additionalRequestedQuantity;
        BaseQuantity += additionalBaseQuantity;
        Notes = notes ?? Notes;
    }
}
