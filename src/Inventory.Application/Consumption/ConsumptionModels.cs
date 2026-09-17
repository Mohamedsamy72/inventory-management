namespace Inventory.Application.Consumption;

/// <summary>docs/04 §12 - a purely statistical log; ZERO stock effect. No document-number
/// sequence exists for this record (docs/29 does not migrate one), matching the entity's own
/// shape - unlike every other Phase 8+ document, this is not part of the DSC-/CNT-/REC- family.</summary>
public sealed record RecordConsumptionCommand(Guid RestaurantId, Guid ItemId, decimal Quantity, Guid UnitId, DateOnly ConsumptionDate, string? Notes);

public sealed record ConsumptionRecordSummary(
    Guid Id, Guid RestaurantId, Guid ItemId, decimal Quantity, Guid UnitId, DateOnly ConsumptionDate,
    Guid RecordedBy, string? Notes, DateTimeOffset CreatedAt);
