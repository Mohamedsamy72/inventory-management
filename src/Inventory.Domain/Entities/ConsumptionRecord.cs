using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>A restaurant's logged consumption. Statistical only - ZERO stock effect
/// (docs/02 section 3.D). Immutable once written; a correction is a new record.</summary>
public sealed class ConsumptionRecord : Entity, ITenantScopedEntity
{
    private ConsumptionRecord()
    {
    }

    public ConsumptionRecord(Guid companyId, Guid restaurantId, Guid itemId, decimal quantity, Guid unitId, decimal baseQuantity, DateOnly consumptionDate, Guid recordedBy, string? notes)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Consumption quantity must be positive.");
        }

        Id = Guid.NewGuid();
        CompanyId = companyId;
        RestaurantId = restaurantId;
        ItemId = itemId;
        Quantity = quantity;
        UnitId = unitId;
        BaseQuantity = baseQuantity;
        ConsumptionDate = consumptionDate;
        RecordedBy = recordedBy;
        Notes = notes;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid ItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal BaseQuantity { get; private set; }
    public DateOnly ConsumptionDate { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
