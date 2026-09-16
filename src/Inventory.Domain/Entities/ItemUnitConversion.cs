using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>
/// Resolves a client-entered unit to the item's base unit (ADR-023). Immutable once used - a
/// correction is a new row with the old one deactivated, never an in-place edit.
/// </summary>
public sealed class ItemUnitConversion : Entity, ITenantScopedEntity
{
    private ItemUnitConversion()
    {
    }

    public ItemUnitConversion(Guid companyId, Guid itemId, Guid fromUnitId, Guid toBaseUnitId, decimal conversionFactor)
    {
        if (conversionFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(conversionFactor), conversionFactor, "Conversion factor must be positive.");
        }

        Id = Guid.NewGuid();
        CompanyId = companyId;
        ItemId = itemId;
        FromUnitId = fromUnitId;
        ToBaseUnitId = toBaseUnitId;
        ConversionFactor = conversionFactor;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid FromUnitId { get; private set; }
    public Guid ToBaseUnitId { get; private set; }
    public decimal ConversionFactor { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
