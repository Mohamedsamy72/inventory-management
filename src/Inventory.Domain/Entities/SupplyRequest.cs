using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// A restaurant's multi-item requisition. <see cref="WarehouseId"/> is DERIVED server-side from
/// the restaurant's default serving warehouse (ADR-028) - the constructor takes it as a plain
/// value because deriving and validating it (checking the warehouse is active) needs data this
/// entity does not have; that resolution belongs to the Phase 9 application handler, never to a
/// client-supplied value. Creating or submitting a request has ZERO stock effect.
/// </summary>
public sealed class SupplyRequest : Entity, ITenantScopedEntity
{
    private SupplyRequest()
    {
    }

    public SupplyRequest(Guid companyId, Guid restaurantId, Guid warehouseId, string documentNumber, Guid requestedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        RestaurantId = restaurantId;
        WarehouseId = warehouseId;
        DocumentNumber = documentNumber;
        Status = SupplyRequestStatus.Draft;
        RequestedBy = requestedBy;
        RequestedAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public SupplyRequestStatus Status { get; private set; }
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Submit()
    {
        if (Status != SupplyRequestStatus.Draft)
        {
            throw new InvalidOperationException($"SupplyRequest {Id} is {Status}; only a Draft request may be submitted.");
        }

        Status = SupplyRequestStatus.Submitted;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status is SupplyRequestStatus.Fulfilled or SupplyRequestStatus.Cancelled)
        {
            throw new InvalidOperationException($"SupplyRequest {Id} is {Status} and cannot be cancelled.");
        }

        Status = SupplyRequestStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the aggregate fulfilment status computed by the application layer from this
    /// request's lines. Restricted to the two fulfilment outcomes - draft/submitted/cancelled
    /// transitions go through their own dedicated methods.
    /// </summary>
    public void UpdateFulfillmentStatus(SupplyRequestStatus newStatus)
    {
        if (newStatus is not (SupplyRequestStatus.PartiallyFulfilled or SupplyRequestStatus.Fulfilled))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), newStatus, "Only PartiallyFulfilled or Fulfilled may be set via fulfilment.");
        }

        if (Status != SupplyRequestStatus.Submitted && Status != SupplyRequestStatus.PartiallyFulfilled)
        {
            throw new InvalidOperationException($"SupplyRequest {Id} is {Status}; fulfilment status can only advance from Submitted or PartiallyFulfilled.");
        }

        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
