using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>An explicit "this restaurant may request goods from this warehouse" mapping (product
/// decision superseding ADR-028's single default serving warehouse). Managed by Owner/Admin
/// (`restaurants:manage`); a supply request may only target a warehouse mapped here (and Active,
/// same company). Both sides are tenant-checked structurally by composite FKs.</summary>
public sealed class RestaurantWarehouse : Entity, ITenantScopedEntity
{
    private RestaurantWarehouse()
    {
    }

    public RestaurantWarehouse(Guid companyId, Guid restaurantId, Guid warehouseId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        RestaurantId = restaurantId;
        WarehouseId = warehouseId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
