namespace Inventory.Domain.Common;

/// <summary>
/// Marks an entity as belonging to exactly one tenant (company). Every implementation gets a
/// global EF Core query filter and a composite <c>(company_id, id)</c> key so a cross-tenant
/// reference is structurally impossible, not merely filtered (ADR-016).
/// </summary>
public interface ITenantScopedEntity
{
    Guid CompanyId { get; }
}
