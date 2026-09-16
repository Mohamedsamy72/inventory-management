namespace Inventory.Domain.Enums;

/// <summary>
/// docs/31 section 4.6 canonical vocabulary (ADR-017). <c>Discrepancy</c> is deliberately NOT a
/// status here - it is a separate record type, never a state a Supply can be in.
/// </summary>
public enum SupplyStatus
{
    Prepared,
    Dispatched,
    Confirmed,
    ConfirmedWithDiscrepancy,
    RejectedAtDelivery,
    Cancelled,
}
