using Inventory.Domain.Enums;

namespace Inventory.Application.Receiving;

public sealed record CreateReceivingOrderCommand(Guid WarehouseId, Guid? SupplierId, DateOnly BusinessDate);

/// <summary>The unit cost is entered here, at draft time - a receiving line records what the
/// warehouse expects to receive AND at what negotiated/invoiced cost (docs/15 §1 point 1: cost
/// originates exclusively from posted receiving orders), not invented later at submission.</summary>
public sealed record AddReceivingOrderLineCommand(Guid ItemId, Guid UnitId, decimal ExpectedQuantity, decimal UnitCost, string? Notes);

public sealed record VerifyReceivingOrderLineCommand(Guid LineId, decimal ActualQuantity);

public sealed record VerifyReceivingOrderCommand(IReadOnlyList<VerifyReceivingOrderLineCommand> Lines);

public sealed record ReceivingOrderLineSummary(
    Guid Id, Guid ItemId, decimal ExpectedQuantity, decimal? ActualQuantity, Guid UnitId,
    decimal? UnitCost, decimal? TotalCost, bool Reconciled);

public sealed record ReceivingOrderSummary(
    Guid Id, string DocumentNumber, Guid WarehouseId, Guid? SupplierId, ReceivingOrderStatus Status,
    DateOnly BusinessDate, Guid? ReversedBy, string? ReversalReason, IReadOnlyList<ReceivingOrderLineSummary> Lines);

public sealed record WarehouseStockLine(Guid ItemId, decimal Balance, decimal InTransit, decimal Available, decimal? AverageUnitCost);
