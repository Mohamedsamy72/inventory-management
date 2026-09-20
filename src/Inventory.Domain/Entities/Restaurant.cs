using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// A consumption node: supply requests, confirmed receipts, consumption logs, discrepancy
/// records - and nothing else. A restaurant NEVER holds a stock balance (docs/02 section 1).
///
/// Product decision (Change 1) reversed the original ADR-028 model: a restaurant is no longer
/// pinned to one permanent "serving warehouse" - it can receive from more than one. Which
/// warehouse a given supply request targets is chosen (and validated) at the point that request
/// is created, not carried as a restaurant-level property.
/// </summary>
public sealed class Restaurant : Entity, ITenantScopedEntity
{
    private Restaurant()
    {
    }

    public Restaurant(Guid companyId, string nameArabic, string code, string? address, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        NameArabic = nameArabic;
        Code = code;
        Status = RestaurantStatus.Active;
        Address = address;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public RestaurantStatus Status { get; private set; }
    public string? Address { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string nameArabic, string? address, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        NameArabic = nameArabic;
        Address = address;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        Status = RestaurantStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        Status = RestaurantStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
