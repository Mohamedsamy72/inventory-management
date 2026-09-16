using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>Grants a Warehouse Staff user visibility into one warehouse.</summary>
public sealed class UserWarehouseScope : Entity, ITenantScopedEntity
{
    private UserWarehouseScope()
    {
    }

    public UserWarehouseScope(Guid companyId, Guid userId, Guid warehouseId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        UserId = userId;
        WarehouseId = warehouseId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
