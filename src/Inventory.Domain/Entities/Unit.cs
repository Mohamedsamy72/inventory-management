using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class Unit : Entity, ITenantScopedEntity
{
    private Unit()
    {
    }

    public Unit(Guid companyId, string nameArabic, string? abbreviation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        NameArabic = nameArabic;
        Abbreviation = abbreviation;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public string? Abbreviation { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string nameArabic, string? abbreviation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        NameArabic = nameArabic;
        Abbreviation = abbreviation;
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
