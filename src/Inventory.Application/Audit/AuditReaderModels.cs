namespace Inventory.Application.Audit;

/// <summary>Task 14.3 (docs/17 §1.3): when <see cref="From"/> is not supplied, the reader
/// service applies a bounded default window (30 days) rather than scanning the whole table -
/// this is where that default is applied, not the endpoint.</summary>
public sealed record AuditLogFilter(Guid? ActorUserId, string? EntityType, DateTimeOffset? From, DateTimeOffset? To);

public sealed record AuditLogSummary(
    Guid Id, Guid ActorUserId, string ActorRole, string Action, string EntityType, Guid EntityId,
    string DescriptionArabic, string? OldValuesJson, string? NewValuesJson, string Result,
    Guid? WarehouseId, Guid? RestaurantId, string CorrelationId, DateTimeOffset CreatedAt);

/// <summary>Task 14.2 - the reduced, purely human-readable projection: no entity ids, no
/// old/new-value JSON, nothing an Owner reading a plain activity feed needs to parse.</summary>
public sealed record AuditActivitySummary(Guid Id, string DescriptionArabic, string ActorRole, string Result, DateTimeOffset CreatedAt);
