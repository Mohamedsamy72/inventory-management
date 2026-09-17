using Inventory.Domain.Enums;

namespace Inventory.Application.Supplies;

/// <summary>One request line to fulfil now, in that line's OWN unit (not base) - the same unit
/// docs/09 task 9.5 already resolved a conversion for, so no new unit choice is offered here. A
/// line omitted from the command is simply left unfulfilled THIS round (docs/09's
/// `PartiallyFulfilled` status exists precisely because fulfilment can happen over several
/// separate `Supply` documents as stock becomes available); <see cref="FulfilledQuantity"/> of
/// `0` is explicitly valid (task 10.3 - out of stock right now).</summary>
public sealed record FulfillSupplyLineCommand(Guid SupplyRequestItemId, decimal FulfilledQuantity);

public sealed record FulfillSupplyRequestCommand(IReadOnlyList<FulfillSupplyLineCommand> Lines);

public sealed record SupplyLineSummary(Guid Id, Guid ItemId, decimal DispatchedQuantity, decimal? ReceivedQuantity, Guid UnitId);

public sealed record SupplySummary(
    Guid Id, string DocumentNumber, Guid WarehouseId, Guid RestaurantId, Guid? SupplyRequestId, SupplyStatus Status,
    Guid? PreparedBy, DateTimeOffset? PreparedAt, Guid? DispatchedBy, DateTimeOffset? DispatchedAt,
    IReadOnlyList<SupplyLineSummary> Lines);

/// <summary>Task 11.2 - the actual received quantity for one supply line, in that line's own
/// unit (matching how it was dispatched). `0` is explicitly valid (a fully rejected line).</summary>
public sealed record ConfirmSupplyLineCommand(Guid SupplyItemId, decimal ReceivedQuantity);

public sealed record ConfirmSupplyCommand(IReadOnlyList<ConfirmSupplyLineCommand> Lines);

/// <summary>Task 10.6 (ADR-019): the item ids whose fulfilled/dispatched base quantity exceeded
/// `available` (balance − in-transit) AT THE MOMENT of this specific operation - a live,
/// point-in-time advisory, never persisted, never blocking, never a reservation. Only the
/// Fulfil/Dispatch response carries this; a later `GET` returns the plain <see cref="SupplySummary"/>
/// with no advisory recomputation, since "was this short when it happened" is what the acting
/// user needs to see, not a stale flag that silently changes meaning as the balance moves.</summary>
public sealed record SupplyOperationResult(SupplySummary Supply, IReadOnlyList<Guid> InsufficientStockItemIds);
