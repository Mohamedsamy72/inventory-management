namespace Inventory.Application.Common;

/// <summary>
/// Task 7.12 (ADR-018) - the quantity "قيد النقل" (in transit): goods a warehouse has dispatched
/// but a restaurant has not yet confirmed. Because dispatch never deducts stock (ADR-019), those
/// goods still count as part of the warehouse balance - this figure exists to let a stock count
/// subtract them back out, so the counter reconciles against what is physically on the shelf,
/// not what the ledger says minus goods already on a truck. Always derived on demand from
/// <c>supply_items</c> whose parent <c>Supply</c> is <c>Dispatched</c>; never persisted as a
/// balance row, and always attributed to the WAREHOUSE, never to a restaurant (the
/// no-restaurant-inventory invariant is untouched).
/// </summary>
public interface IInTransitCalculator
{
    /// <summary>The in-transit base quantity for one item in one warehouse. Zero when nothing
    /// dispatched from that warehouse for that item is currently awaiting confirmation.</summary>
    Task<decimal> GetInTransitQuantityAsync(Guid warehouseId, Guid itemId, CancellationToken cancellationToken);

    /// <summary>Every item in one warehouse with a non-zero in-transit quantity - the shape a
    /// stock-count screen needs (subtract per line), rather than one call per item.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetInTransitQuantitiesByItemAsync(Guid warehouseId, CancellationToken cancellationToken);
}
