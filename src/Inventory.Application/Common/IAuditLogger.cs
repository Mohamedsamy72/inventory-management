namespace Inventory.Application.Common;

/// <summary>
/// Task 4.11 - queues one audit row for the CURRENT unit of work. Deliberately does not call
/// <c>SaveChangesAsync</c> itself: the audit row must be written inside the same database
/// transaction as the business change it records (docs/14 §1), and the only way to guarantee
/// that without a distributed transaction is to add both to the SAME <c>DbContext</c> and let
/// the caller's own single <c>SaveChangesAsync</c> flush them together. A rolled-back business
/// operation therefore leaves no audit trace, automatically - the implementation does not need
/// to detect a rollback, because there was never a separate commit for it to roll back from.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Adds one audit row to the current <c>DbContext</c>'s change tracker. Not
    /// persisted until the caller's own <c>SaveChangesAsync</c> runs.</summary>
    void Record(AuditEntry entry);
}
