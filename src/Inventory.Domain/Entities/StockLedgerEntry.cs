using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// One append-only row in the stock ledger - THE source of truth for inventory (docs/04 section
/// 7.1). Fully immutable after construction: no setter, no mutation method exists anywhere on
/// this type. A correction is a new reversing row, never an edit (docs/02 invariant 6). Database
/// triggers back this up (docs/29 section 4.4) so the guarantee holds even against a bug or a
/// stray script, not just against this class's own API.
/// </summary>
public sealed class StockLedgerEntry : Entity, ITenantScopedEntity
{
    private StockLedgerEntry()
    {
    }

    public StockLedgerEntry(
        Guid companyId,
        Guid warehouseId,
        Guid itemId,
        MovementType movementType,
        decimal quantity,
        decimal baseQuantity,
        Guid unitId,
        ReferenceType referenceType,
        Guid referenceId,
        decimal? unitCost,
        decimal? totalCost,
        Guid actorUserId,
        DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        WarehouseId = warehouseId;
        ItemId = itemId;
        MovementType = movementType;
        Quantity = quantity;
        BaseQuantity = baseQuantity;
        UnitId = unitId;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        UnitCost = unitCost;
        TotalCost = totalCost;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ItemId { get; private set; }
    public MovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public Guid UnitId { get; private set; }
    public ReferenceType ReferenceType { get; private set; }
    public Guid ReferenceId { get; private set; }

    /// <summary>Populated for every movement type, including those where WAC does not move
    /// (ADR-020 point 15) - a cost not captured at posting time is unrecoverable.</summary>
    public decimal? UnitCost { get; private set; }

    public decimal? TotalCost { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
