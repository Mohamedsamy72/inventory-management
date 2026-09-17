using Inventory.Application.Common;

namespace Inventory.Application.Supplies;

/// <summary>Phase 10 - fulfilment and dispatch (docs/09 tasks 10.1-10.8, ADR-017). Neither
/// method here calls <see cref="IStockPostingService"/> - fulfilment and dispatch have ZERO
/// stock effect; only restaurant receipt confirmation (Phase 11) posts a ledger row.</summary>
public interface ISupplyService
{
    /// <summary>Transaction T5 (docs/30 §4): allocates a `SUP-` number, creates the `Supply` in
    /// `Prepared`, links each fulfilled line's `SupplyItem` back to its `SupplyRequestItem`
    /// (CR-024), and advances the request to `PartiallyFulfilled`/`Fulfilled`. The originating
    /// request must be `Submitted` or `PartiallyFulfilled`.</summary>
    Task<TransactionalResult<SupplyOperationResult>> FulfillAsync(Guid supplyRequestId, FulfillSupplyRequestCommand command, CancellationToken cancellationToken);

    Task<SupplySummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<SupplySummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Transaction T6: `Prepared -> Dispatched`, actor and timestamp only.</summary>
    Task<TransactionalResult<SupplyOperationResult>> DispatchAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>`Prepared` only (the entity's own guard) - never after dispatch.</summary>
    Task<TransactionalResult<SupplySummary>> CancelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Transaction T7 (docs/30 §7.1) - the ONLY stock-deducting path in the product.
    /// Posts `RESTAURANT_RECEIPT_CONFIRMED` for the actual received quantity per line (valued at
    /// the current WAC, which stays unchanged - ADR-020), creates a line-level
    /// `SupplyReceiptVariance` <c>Discrepancy</c> for every non-zero variance, and sets
    /// the terminal status: all-full -&gt; `Confirmed`; any variance -&gt; `ConfirmedWithDiscrepancy`;
    /// all zero -&gt; `RejectedAtDelivery` with no ledger row at all (docs/04 §11 - the warehouse
    /// balance already reflects the goods, since dispatch never deducted them). Any short line's
    /// `InsufficientStockException` rolls back the ENTIRE confirmation - nothing partial is ever
    /// written (docs/30 §7.1's "impossible confirmation": the supply stays `Dispatched`,
    /// resolved only by a later physical stock count, never an auto-adjustment).</summary>
    Task<TransactionalResult<SupplyOperationResult>> ConfirmAsync(Guid id, ConfirmSupplyCommand command, IdempotencyContext idempotency, CancellationToken cancellationToken);
}
