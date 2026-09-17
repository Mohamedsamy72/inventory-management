using Inventory.Domain.Enums;

namespace Inventory.Application.Common;

/// <summary>
/// Task 7.3 - the ONLY path that writes <c>stock_ledger</c> or <c>stock_balances</c> (enforced by
/// the <c>StockPostingRules</c> architecture test, task 7.13). Every later phase that moves stock
/// (receiving, confirmation, stock counts) calls this instead of touching either table directly.
/// </summary>
/// <remarks>
/// The caller MUST open its own explicit database transaction before calling
/// <see cref="PostAsync"/> and commit it only after everything else the same business operation
/// needs (document status, audit entry, idempotency record) is also saved - some lines post via
/// a raw SQL statement that executes immediately (docs/30 §5.1's conditional atomic deduction),
/// which would otherwise commit independently of the rest of the operation, the same reason
/// <see cref="IDocumentSequenceService"/> requires an ambient transaction. This method itself
/// calls <c>SaveChangesAsync</c> once, at the end, to flush the tracked-entity lines (task 7.5's
/// xmin-protected WAC recompute) - the caller's own later <c>SaveChangesAsync</c> calls for the
/// rest of the operation still participate in the same transaction.
/// </remarks>
public interface IStockPostingService
{
    /// <summary>Posts every line in <paramref name="lines"/> as one logical unit. Lines are
    /// internally reordered ascending by <see cref="StockPostingLine.ItemId"/> before processing
    /// (task 7.6: deterministic lock ordering eliminates deadlock between two postings sharing
    /// items) - callers do not need to pre-sort. Throws <see cref="InsufficientStockException"/>
    /// the instant any line's deduction cannot be satisfied, leaving everything already
    /// processed in this call for the caller's transaction to roll back.</summary>
    Task PostAsync(IReadOnlyList<StockPostingLine> lines, CancellationToken cancellationToken);
}

/// <summary>One movement to post. <see cref="Quantity"/>/<see cref="UnitId"/> are what the
/// caller's user entered; <see cref="BaseQuantity"/> is what actually moves the balance - already
/// resolved by the caller via <see cref="IUnitConversionResolver"/> before calling here (docs/25:
/// a base quantity is always server-derived, never accepted from a client, and this service has
/// no unit-conversion knowledge of its own). Both quantities are SIGNED: positive increases the
/// balance, negative decreases it.</summary>
public sealed record StockPostingLine(
    Guid WarehouseId,
    Guid ItemId,
    Guid UnitId,
    decimal Quantity,
    decimal BaseQuantity,
    MovementType MovementType,
    ReferenceType ReferenceType,
    Guid ReferenceId,
    /// <summary>Required (non-null) for <see cref="MovementType.OpeningBalance"/>,
    /// <see cref="MovementType.IncomingPosted"/>, and <see cref="MovementType.IncomingReconciliation"/>
    /// - the cost the WAC recompute blends in, or (for reconciliation) the originating line's own
    /// cost (ADR-020). Ignored for every other movement type, which values itself at the current
    /// WAC instead.</summary>
    decimal? UnitCost);

/// <summary>docs/30 §5.1/ADR-021 - the conditional atomic deduction found zero eligible rows: not
/// enough stock (or none at all) for this item/warehouse. The caller's transaction must roll
/// back entirely; nothing about this specific line, or any other line already processed in the
/// same <see cref="IStockPostingService.PostAsync"/> call, may be left partially applied.</summary>
public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(Guid itemId, Guid warehouseId)
        : base($"Insufficient stock for item {itemId} in warehouse {warehouseId}.")
    {
        ItemId = itemId;
        WarehouseId = warehouseId;
    }

    public Guid ItemId { get; }
    public Guid WarehouseId { get; }
}
