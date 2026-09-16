using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>The only entity type that ever holds stock (docs/02 section 1 - inviolable).</summary>
public sealed class Warehouse : Entity, ITenantScopedEntity
{
    private Warehouse()
    {
    }

    public Warehouse(Guid companyId, string nameArabic, string code, string? address, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        NameArabic = nameArabic;
        Code = code;
        Status = WarehouseStatus.Active;
        Address = address;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public WarehouseStatus Status { get; private set; }
    public string? Address { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Deactivate()
    {
        Status = WarehouseStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        Status = WarehouseStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
