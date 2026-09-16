using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>
/// A materialised projection of <see cref="StockLedgerEntry"/> for fast querying and
/// concurrency locking - never authoritative on its own (docs/04 section 7.1). Concurrency is
/// enforced via the PostgreSQL <c>xmin</c> system column, configured in the EF mapping, not a
/// column on this entity (ADR-022).
/// <para>
/// The full posting/costing engine (the only legitimate writer, WAC recomputation) arrives in
/// Phase 7 (<c>IStockPostingService</c>). <see cref="SetQuantityAndCost"/> exists only so this
/// entity is usable now; Phase 7 adds the architecture test restricting who may call it.
/// </para>
/// </summary>
public sealed class StockBalance : Entity, ITenantScopedEntity
{
    private StockBalance()
    {
    }

    public StockBalance(Guid companyId, Guid warehouseId, Guid itemId, Guid baseUnitId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        WarehouseId = warehouseId;
        ItemId = itemId;
        BaseUnitId = baseUnitId;
        Quantity = 0m;
        AverageUnitCost = 0m;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid BaseUnitId { get; private set; }
    public decimal AverageUnitCost { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void SetQuantityAndCost(decimal quantity, decimal averageUnitCost)
    {
        if (quantity < 0)
        {
            throw new InvalidOperationException($"Stock balance for item {ItemId} in warehouse {WarehouseId} cannot go negative (ADR-021). Attempted: {quantity}.");
        }

        Quantity = quantity;
        AverageUnitCost = averageUnitCost;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
