using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class Supplier : Entity, ITenantScopedEntity
{
    private Supplier()
    {
    }

    public Supplier(Guid companyId, string nameArabic, string? phone, string? contactPerson, string? address, string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        NameArabic = nameArabic;
        Phone = phone;
        ContactPerson = contactPerson;
        Address = address;
        Notes = notes;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string nameArabic, string? phone, string? contactPerson, string? address, string? notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        NameArabic = nameArabic;
        Phone = phone;
        ContactPerson = contactPerson;
        Address = address;
        Notes = notes;
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
