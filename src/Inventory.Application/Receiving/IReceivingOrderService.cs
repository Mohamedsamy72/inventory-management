using Inventory.Application.Common;

namespace Inventory.Application.Receiving;

/// <summary>Phase 8 - the first stock-increasing workflow (docs/09 tasks 8.1-8.8). Every
/// stock-affecting method delegates to <see cref="IStockPostingService"/> - this service owns
/// the document lifecycle and orchestration only.</summary>
public interface IReceivingOrderService
{
    Task<TransactionalResult<ReceivingOrderSummary>> CreateDraftAsync(CreateReceivingOrderCommand command, CancellationToken cancellationToken);

    Task<ReceivingOrderSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<ReceivingOrderSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Draft only (task 8.3); rejects a second line for the same item (CR-023, backed
    /// by <c>uq_roi_item</c>).</summary>
    Task<TransactionalResult<ReceivingOrderLineSummary>> AddLineAsync(Guid orderId, AddReceivingOrderLineCommand command, CancellationToken cancellationToken);

    Task<TransactionalResult<bool>> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken cancellationToken);

    /// <summary>Transaction T1 (docs/30 §4): posts `INCOMING_POSTED` for every line via
    /// <see cref="IStockPostingService"/>, then `Draft → Submitted`. <paramref name="idempotency"/>
    /// is recorded inside this same transaction (docs/30 §6.2 step 6) - required because the
    /// endpoint marks this route `.RequireIdempotencyKey()`.</summary>
    Task<TransactionalResult<ReceivingOrderSummary>> SubmitAsync(Guid orderId, IdempotencyContext idempotency, CancellationToken cancellationToken);

    /// <summary>Transaction T2: posts `INCOMING_RECONCILIATION` for the DELTA only
    /// (actual − expected) per line, valued at that line's own `unit_cost` (ADR-020) - never the
    /// full actual quantity (docs/09 §Phase 8 "Risks": this is the single most likely defect in
    /// the product). Then `Submitted → Verified`.</summary>
    Task<TransactionalResult<ReceivingOrderSummary>> VerifyAsync(Guid orderId, VerifyReceivingOrderCommand command, IdempotencyContext idempotency, CancellationToken cancellationToken);

    /// <summary>Transaction T3: permitted from `Submitted` or `Verified`, never from `Reversed`
    /// (enforced by the entity itself). Reverses every ledger effect already posted.</summary>
    Task<TransactionalResult<ReceivingOrderSummary>> ReverseAsync(Guid orderId, string reason, IdempotencyContext idempotency, CancellationToken cancellationToken);

    /// <summary>Task 8.11 - balance/in-transit/available for one warehouse, every item that has
    /// a balance row.</summary>
    Task<IReadOnlyList<WarehouseStockLine>> GetWarehouseStockAsync(Guid warehouseId, CancellationToken cancellationToken);
}
