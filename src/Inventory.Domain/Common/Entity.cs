namespace Inventory.Domain.Common;

/// <summary>Base type for every domain entity. The primary key is DB-generated (gen_random_uuid()).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }
}
