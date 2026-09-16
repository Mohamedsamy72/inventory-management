using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>One tenant: a restaurant group owning many warehouses and restaurant branches.</summary>
public sealed class Company : Entity
{
    private Company()
    {
    }

    public Company(string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Id = Guid.NewGuid();
        Name = name;
        Code = code;
        Status = CompanyStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public CompanyStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Suspend()
    {
        Status = CompanyStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        Status = CompanyStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
