using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>Grants a Restaurant Supervisor user visibility into one restaurant branch.</summary>
public sealed class UserRestaurantScope : Entity, ITenantScopedEntity
{
    private UserRestaurantScope()
    {
    }

    public UserRestaurantScope(Guid companyId, Guid userId, Guid restaurantId)
    {
        Id = Guid.NewGuid();
        CompanyId = companyId;
        UserId = userId;
        RestaurantId = restaurantId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
