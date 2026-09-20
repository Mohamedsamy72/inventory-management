using Inventory.Domain.Enums;

namespace Inventory.Application.SupplyRequests;

/// <summary>ADR-028: deliberately NO WarehouseId property. The handler derives the serving
/// warehouse server-side from the restaurant's own configuration - a client-supplied value here
/// would reintroduce exactly the authorization hazard the ADR removes (docs/09 task 9.8).</summary>
/// <summary>Change 1 (reversed ADR-028): <see cref="WarehouseId"/> is now client-chosen, not
/// derived from the restaurant - validated in <c>SupplyRequestService.CreateDraftAsync</c>
/// against the caller's own tenant (via the already-tenant-filtered <c>Warehouses</c> DbSet) and
/// `Active` status.</summary>
public sealed record CreateSupplyRequestCommand(Guid RestaurantId, Guid WarehouseId);

public sealed record AddSupplyRequestItemCommand(Guid ItemId, Guid UnitId, decimal RequestedQuantity, string? Notes);

public sealed record UpdateSupplyRequestItemCommand(Guid UnitId, decimal RequestedQuantity, string? Notes);

public sealed record SupplyRequestLineSummary(Guid Id, Guid ItemId, decimal RequestedQuantity, decimal FulfilledQuantity, Guid UnitId, string? Notes);

public sealed record SupplyRequestSummary(
    Guid Id, string DocumentNumber, Guid RestaurantId, Guid WarehouseId, SupplyRequestStatus Status,
    Guid RequestedBy, DateTimeOffset RequestedAt, IReadOnlyList<SupplyRequestLineSummary> Lines);
