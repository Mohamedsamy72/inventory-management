using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class StockCountItem : Entity
{
    private StockCountItem()
    {
    }

    public StockCountItem(Guid stockCountId, Guid itemId, decimal systemQuantity, Guid baseUnitId, string? notes)
    {
        Id = Guid.NewGuid();
        StockCountId = stockCountId;
        ItemId = itemId;
        SystemQuantity = systemQuantity;
        BaseUnitId = baseUnitId;
        Notes = notes;
    }

    public Guid StockCountId { get; private set; }
    public Guid ItemId { get; private set; }

    /// <summary>The expected physical figure = ledger balance minus in-transit (ADR-018), not
    /// the raw ledger balance. Snapshotted when the count is opened.</summary>
    public decimal SystemQuantity { get; private set; }

    public decimal? PhysicalQuantity { get; private set; }
    public decimal? Variance { get; private set; }
    public Guid BaseUnitId { get; private set; }
    public string? Notes { get; private set; }

    public void RecordPhysicalCount(decimal physicalQuantity)
    {
        PhysicalQuantity = physicalQuantity;
        Variance = physicalQuantity - SystemQuantity;
    }
}
