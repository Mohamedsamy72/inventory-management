using Inventory.Domain.Enums;

namespace Inventory.Application.StockCounts;

public sealed record CreateStockCountCommand(Guid WarehouseId, bool IsBlindCount);

/// <summary>Quantities are entered directly in the item's own base unit (matching
/// <see cref="StockCountLineSummary.BaseUnitId"/>) - unlike receiving/supply, a physical count
/// has no separate display-unit concept to resolve.</summary>
public sealed record RecordStockCountLineCommand(Guid LineId, decimal PhysicalQuantity);

public sealed record RecordStockCountCommand(IReadOnlyList<RecordStockCountLineCommand> Lines);

/// <summary>Task 13.4: <see cref="SystemQuantity"/> and <see cref="Variance"/> are null here -
/// not merely omitted client-side - whenever the parent count is a blind count still being
/// counted (docs/09's own risk: implementing this only in the UI would leak the system quantity
/// over the wire and defeat the control entirely).</summary>
public sealed record StockCountLineSummary(
    Guid Id, Guid ItemId, decimal? SystemQuantity, decimal? PhysicalQuantity, decimal? Variance, Guid BaseUnitId, string? Notes);

public sealed record StockCountSummary(
    Guid Id, string DocumentNumber, Guid WarehouseId, StockCountStatus Status, bool IsBlindCount,
    Guid OpenedBy, DateTimeOffset OpenedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt,
    IReadOnlyList<StockCountLineSummary> Lines);
