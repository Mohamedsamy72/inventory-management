using Inventory.Application.Common;

namespace Inventory.Application.StockCounts;

/// <summary>Phase 13 - reconciling ledger balances with physical reality (docs/09 tasks
/// 13.1-13.11). The only phase after Phase 7 that legitimately calls
/// <see cref="IStockPostingService"/> with <c>PhysicalAdjustment</c>, and only from
/// <see cref="ApproveAsync"/>.</summary>
public interface IStockCountService
{
    /// <summary>Task 13.2-13.3: allocates a `CNT-` number and snapshots every item currently
    /// holding a balance in the warehouse - `SystemQuantity` = ledger balance MINUS in-transit
    /// (ADR-018, via <c>IInTransitCalculator</c>), not the raw balance. Moves straight to
    /// `InProgress` (task 13.4 assumes an in-progress count to record against; nothing in this
    /// phase's task list needs a separate zero-duration `Draft` action).</summary>
    Task<TransactionalResult<StockCountSummary>> CreateAsync(CreateStockCountCommand command, CancellationToken cancellationToken);

    Task<StockCountSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<StockCountSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Task 13.4-13.5: `InProgress` only. Accepts a subset of lines - physical counting
    /// is naturally incremental (different aisles, different times); <see cref="SubmitForApprovalAsync"/>
    /// is the explicit signal that counting is done.</summary>
    Task<TransactionalResult<StockCountSummary>> RecordAsync(Guid id, RecordStockCountCommand command, CancellationToken cancellationToken);

    /// <summary>`InProgress -> PendingApproval`.</summary>
    Task<TransactionalResult<StockCountSummary>> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Transaction T8 (task 13.6): Owner/Admin only (enforced by the endpoint's
    /// permission, not here), idempotency required. Posts `PHYSICAL_ADJUSTMENT` per non-zero
    /// variance (valued at the current WAC, WAC unchanged - task 13.8, ADR-020; a negative
    /// adjustment breaching zero fails with `InsufficientStockException` - task 13.9, ADR-021 -
    /// both already guaranteed by <see cref="IStockPostingService"/>'s existing dispatch logic,
    /// not new logic here), creates a `StockCountVariance` <c>Discrepancy</c> per non-zero line,
    /// and locks every line against further edits (task 13.10 - enforced by the `Approved`
    /// status itself, since <see cref="RecordAsync"/> only accepts `InProgress`).</summary>
    Task<TransactionalResult<StockCountSummary>> ApproveAsync(Guid id, IdempotencyContext idempotency, CancellationToken cancellationToken);

    /// <summary>Task 13.7: `PendingApproval -> InProgress`, zero stock effect - no
    /// <see cref="IStockPostingService"/> call anywhere in this method.</summary>
    Task<TransactionalResult<StockCountSummary>> RejectAsync(Guid id, CancellationToken cancellationToken);
}
