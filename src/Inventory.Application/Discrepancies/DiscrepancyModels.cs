using Inventory.Domain.Enums;

namespace Inventory.Application.Discrepancies;

public sealed record DiscrepancySummary(
    Guid Id, string DocumentNumber, DiscrepancyType Type, ReferenceType ReferenceType, Guid ReferenceId,
    Guid? ReferenceLineId, Guid? WarehouseId, Guid? RestaurantId, Guid? ItemId,
    decimal ExpectedQuantity, decimal ActualQuantity, decimal Variance, DiscrepancyStatus Status,
    string? Reason, Guid? ResolvedBy, DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt);

public sealed record ResolveDiscrepancyCommand(string Reason);
