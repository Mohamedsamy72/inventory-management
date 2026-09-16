namespace Inventory.Domain.Enums;

/// <summary>The four sources of variance the product produces (docs/09 section 9, Phase 12).</summary>
public enum DiscrepancyType
{
    ReceivingVariance,
    SupplyReceiptVariance,
    StockCountVariance,
    StockUnavailableAtConfirmation,
}
