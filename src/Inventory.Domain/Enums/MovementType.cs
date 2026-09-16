namespace Inventory.Domain.Enums;

/// <summary>
/// The exhaustive set of stock ledger movement types (docs/02 section 4.2). Nothing else may
/// ever be posted: there is no "set stock" operation and no manual balance overwrite.
/// </summary>
public enum MovementType
{
    OpeningBalance,
    IncomingPosted,
    IncomingReconciliation,
    RestaurantReceiptConfirmed,
    PhysicalAdjustment,
}
