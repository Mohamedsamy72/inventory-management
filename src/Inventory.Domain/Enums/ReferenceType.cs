namespace Inventory.Domain.Enums;

/// <summary>The document kind a stock ledger row or discrepancy traces back to (docs/04 section 4.2).</summary>
public enum ReferenceType
{
    ReceivingOrder,
    Supply,
    StockCount,
    ManualAdjustment,
}
