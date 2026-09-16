using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// A consumption node: supply requests, confirmed receipts, consumption logs, discrepancy
/// records - and nothing else. A restaurant NEVER holds a stock balance (docs/02 section 1).
/// </summary>
public sealed class Restaurant : Entity, ITenantScopedEntity
{
    private Restaurant()
    {
    }

    public Restaurant(Guid companyId, string nameArabic, string code, Guid defaultServingWarehouseId, string? address, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        NameArabic = nameArabic;
        Code = code;
        Status = RestaurantStatus.Active;
        DefaultServingWarehouseId = defaultServingWarehouseId;
        Address = address;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public RestaurantStatus Status { get; private set; }

    /// <summary>
    /// ADR-028: exactly one serving warehouse. Resolved server-side for every supply request at
    /// the moment of creation - a later change here does NOT retroactively redirect requests
    /// already created (SW-8).
    /// </summary>
    public Guid DefaultServingWarehouseId { get; private set; }

    public string? Address { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void ChangeServingWarehouse(Guid warehouseId)
    {
        DefaultServingWarehouseId = warehouseId;
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
