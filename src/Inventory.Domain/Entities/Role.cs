using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>One of the exactly five product roles. Global reference data, not tenant-scoped.</summary>
public sealed class Role : Entity
{
    private Role()
    {
    }

    public Role(RoleName name)
    {
        Id = Guid.NewGuid();
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public RoleName Name { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
