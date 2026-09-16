namespace Inventory.Domain.Enums;

/// <summary>docs/31 section 4.6 canonical vocabulary. The Arabic mapping lives there, not here.</summary>
public enum StockCountStatus
{
    Draft,
    InProgress,
    PendingApproval,
    Approved,
    Rejected,
}
