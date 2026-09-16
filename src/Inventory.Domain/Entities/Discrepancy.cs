using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// A single lifecycle for every variance the system produces: Open -&gt; Investigating -&gt;
/// Resolved. Resolution NEVER posts a stock movement - reconciliation of stock happens only
/// through a stock count (ADR-021, docs/09 Phase 12 task 12.5).
/// </summary>
public sealed class Discrepancy : Entity, ITenantScopedEntity
{
    private Discrepancy()
    {
    }

    public Discrepancy(
        Guid companyId,
        string documentNumber,
        DiscrepancyType type,
        ReferenceType referenceType,
        Guid referenceId,
        Guid? referenceLineId,
        Guid? warehouseId,
        Guid? restaurantId,
        Guid? itemId,
        decimal expectedQuantity,
        decimal actualQuantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        DocumentNumber = documentNumber;
        Type = type;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        ReferenceLineId = referenceLineId;
        WarehouseId = warehouseId;
        RestaurantId = restaurantId;
        ItemId = itemId;
        ExpectedQuantity = expectedQuantity;
        ActualQuantity = actualQuantity;
        Variance = actualQuantity - expectedQuantity;
        Status = DiscrepancyStatus.Open;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public DiscrepancyType Type { get; private set; }
    public ReferenceType ReferenceType { get; private set; }
    public Guid ReferenceId { get; private set; }
    public Guid? ReferenceLineId { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? RestaurantId { get; private set; }
    public Guid? ItemId { get; private set; }
    public decimal ExpectedQuantity { get; private set; }
    public decimal ActualQuantity { get; private set; }
    public decimal Variance { get; private set; }
    public DiscrepancyStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void BeginInvestigating()
    {
        if (Status != DiscrepancyStatus.Open)
        {
            throw new InvalidOperationException($"Discrepancy {Id} is {Status}; only Open discrepancies may move to Investigating.");
        }

        Status = DiscrepancyStatus.Investigating;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Resolve(Guid resolvedBy, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == DiscrepancyStatus.Resolved)
        {
            throw new InvalidOperationException($"Discrepancy {Id} is already Resolved.");
        }

        Status = DiscrepancyStatus.Resolved;
        Reason = reason;
        ResolvedBy = resolvedBy;
        ResolvedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
