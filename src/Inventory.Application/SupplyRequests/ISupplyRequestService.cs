using Inventory.Application.Common;

namespace Inventory.Application.SupplyRequests;

/// <summary>Phase 9 - the restaurant-side requisition (docs/09 tasks 9.1-9.14). Every method
/// here has ZERO stock effect (docs/09 explicitly, twice: at create and at submit) - fulfilment
/// and dispatch (Phase 10) are what eventually move stock, and even those don't post a ledger
/// row; only restaurant receipt confirmation (Phase 11) does.</summary>
public interface ISupplyRequestService
{
    /// <summary>ADR-028 (task 9.8): resolves <c>restaurants.default_serving_warehouse_id</c>
    /// server-side and verifies it is Active - <see cref="TransactionalError.ServingWarehouseUnavailable"/>
    /// otherwise, with no fallback to any other warehouse.</summary>
    Task<TransactionalResult<SupplyRequestSummary>> CreateDraftAsync(CreateSupplyRequestCommand command, CancellationToken cancellationToken);

    Task<SupplyRequestSummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KeysetPage<SupplyRequestSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Draft only (task 9.3). A second call for an item already on this request MERGES
    /// into the existing line (task 9.4) rather than being rejected - but only when the unit
    /// matches the existing line's; a different unit for an already-present item is rejected,
    /// since summing quantities across two different units would silently corrupt the total.</summary>
    Task<TransactionalResult<SupplyRequestLineSummary>> AddLineAsync(Guid requestId, AddSupplyRequestItemCommand command, CancellationToken cancellationToken);

    Task<TransactionalResult<SupplyRequestLineSummary>> UpdateLineAsync(Guid requestId, Guid lineId, UpdateSupplyRequestItemCommand command, CancellationToken cancellationToken);

    Task<TransactionalResult<bool>> RemoveLineAsync(Guid requestId, Guid lineId, CancellationToken cancellationToken);

    /// <summary>Transaction T4 (docs/30 §4, docs/09 task 9.6): requires >= 1 line, every quantity
    /// > 0 (guaranteed by construction), every item still Active. Zero stock effect -
    /// `Draft -> Submitted` only.</summary>
    Task<TransactionalResult<SupplyRequestSummary>> SubmitAsync(Guid requestId, CancellationToken cancellationToken);

    /// <summary>docs/04 §8.1: permitted from any status except `Fulfilled`/`Cancelled` (the
    /// entity's own `Cancel()` guard).</summary>
    Task<TransactionalResult<SupplyRequestSummary>> CancelAsync(Guid requestId, CancellationToken cancellationToken);
}
