using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>One line of a <see cref="ReceivingOrder"/>. No company_id of its own - tenant
/// isolation is inherited through its parent (docs/06).</summary>
public sealed class ReceivingOrderItem : Entity
{
    private ReceivingOrderItem()
    {
    }

    public ReceivingOrderItem(Guid receivingOrderId, Guid itemId, decimal expectedQuantity, Guid unitId, decimal baseQuantity, string? notes)
    {
        if (expectedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedQuantity), expectedQuantity, "Expected quantity must be positive.");
        }

        if (baseQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(baseQuantity), baseQuantity, "Base quantity must be positive.");
        }

        Id = Guid.NewGuid();
        ReceivingOrderId = receivingOrderId;
        ItemId = itemId;
        ExpectedQuantity = expectedQuantity;
        UnitId = unitId;
        BaseQuantity = baseQuantity;
        Reconciled = false;
        Notes = notes;
    }

    public Guid ReceivingOrderId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal ExpectedQuantity { get; private set; }
    public decimal? ActualQuantity { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public decimal? ActualBaseQuantity { get; private set; }
    public decimal? UnitCost { get; private set; }
    public decimal? TotalCost { get; private set; }

    /// <summary>Guards against a second verify double-posting the reconciliation (CR-041).</summary>
    public bool Reconciled { get; private set; }

    public string? Notes { get; private set; }

    public void SetPostedCost(decimal unitCost, decimal totalCost)
    {
        UnitCost = unitCost;
        TotalCost = totalCost;
    }

    public void RecordReconciliation(decimal actualQuantity, decimal actualBaseQuantity)
    {
        if (Reconciled)
        {
            throw new InvalidOperationException($"ReceivingOrderItem {Id} was already reconciled; a second reconciliation would double-post (CR-041).");
        }

        ActualQuantity = actualQuantity;
        ActualBaseQuantity = actualBaseQuantity;
        Reconciled = true;
    }
}
