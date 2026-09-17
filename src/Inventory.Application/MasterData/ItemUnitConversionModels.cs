namespace Inventory.Application.MasterData;

/// <summary>No <c>ToBaseUnitId</c> property (task 6.2, ADR-023): the target is always the item's
/// own current base unit, derived server-side - never accepted as input, the same "remove the
/// input, don't just validate it" pattern ADR-028 already established for the supply-request
/// serving warehouse. The database's own composite FK (<c>fk_conversion_item_base_unit</c>,
/// docs/29 §4.3) would reject a mismatched value anyway; not accepting the field at all removes
/// the class of bug rather than one instance of it.</summary>
public sealed record CreateItemUnitConversionCommand(Guid ItemId, Guid FromUnitId, decimal ConversionFactor);

public sealed record ItemUnitConversionSummary(
    Guid Id, Guid ItemId, Guid FromUnitId, Guid ToBaseUnitId, decimal ConversionFactor, bool IsActive, DateTimeOffset CreatedAt);
