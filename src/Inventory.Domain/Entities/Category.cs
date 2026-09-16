using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class Category : Entity, ITenantScopedEntity
{
    private Category()
    {
    }

    public Category(Guid companyId, string nameArabic, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        NameArabic = nameArabic;
        Description = description;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Rename(string nameArabic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        NameArabic = nameArabic;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
