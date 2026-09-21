namespace Inventory.Application.Consumption;

/// <summary>docs/04 §12 - a purely statistical log; ZERO stock effect. No document-number
/// sequence exists for this record (docs/29 does not migrate one), matching the entity's own
/// shape - unlike every other Phase 8+ document, this is not part of the DSC-/CNT-/REC- family.</summary>
public sealed record RecordConsumptionCommand(Guid RestaurantId, Guid ItemId, decimal Quantity, Guid UnitId, DateOnly ConsumptionDate, string? Notes);

public sealed record ConsumptionRecordSummary(
    Guid Id, Guid RestaurantId, Guid ItemId, decimal Quantity, Guid UnitId, DateOnly ConsumptionDate,
    Guid RecordedBy, string? Notes, DateTimeOffset CreatedAt,
    string? ItemName = null, string? UnitName = null, string? RestaurantName = null, string? RecordedByName = null, string? RecordedAtLocal = null);

/// <summary>The resolved reporting window. <see cref="Kind"/> is <c>Last24Hours</c> (a rolling 24 hours ending now, NOT
/// midnight-to-now) or <c>Dates</c> (calendar days in the company timezone, inclusive of both ends).
/// <see cref="FromUtc"/> is inclusive and <see cref="ToUtc"/> is the (inclusive for Last24Hours, exclusive for Dates)
/// end instant actually used in the query - returned so a client/print view shows exactly what was applied.</summary>
public sealed record ConsumptionPeriod(string Kind, DateOnly? From, DateOnly? To, DateTimeOffset FromUtc, DateTimeOffset ToUtc, string TimezoneId);

public sealed record ConsumptionTotal(Guid ItemId, string ItemName, Guid UnitId, string UnitName, decimal Quantity, int RecordCount);

/// <summary>Everything the print view needs for exactly the selected filter: capped rows (the totals are always over the
/// WHOLE filtered set, computed in the database), the period as applied, and the company/scope context.</summary>
public sealed record ConsumptionReport(
    ConsumptionPeriod Period,
    DateTimeOffset GeneratedAt,
    string GeneratedAtLocal,
    string CompanyName,
    Guid? RestaurantId,
    string? RestaurantName,
    IReadOnlyList<ConsumptionRecordSummary> Rows,
    IReadOnlyList<ConsumptionTotal> Totals,
    int TotalRecordCount,
    bool Truncated);
