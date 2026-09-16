using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// The fulfilment -&gt; dispatch -&gt; confirmation document (ADR-017). State machine:
/// Prepared -&gt; Dispatched -&gt; Confirmed | ConfirmedWithDiscrepancy | RejectedAtDelivery;
/// Cancelled only from Prepared. Neither fulfilment nor dispatch touches stock - only
/// confirmation does (docs/02 invariants 8-9), and that posting is Phase 11's job, not this
/// entity's.
/// <para>
/// <see cref="PreparedBy"/>/<see cref="PreparedAt"/> and <see cref="DispatchedBy"/>/
/// <see cref="DispatchedAt"/> are nullable, correcting a docs/06 NOT NULL that predates ADR-017
/// splitting fulfilment and dispatch into two separate acts (docs/06's own banner already flags
/// its status comment as stale here) - a Supply is created Prepared, before either has happened.
/// </para>
/// </summary>
public sealed class Supply : Entity, ITenantScopedEntity
{
    private Supply()
    {
    }

    public Supply(Guid companyId, Guid warehouseId, Guid restaurantId, Guid? supplyRequestId, string documentNumber, Guid preparedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        WarehouseId = warehouseId;
        RestaurantId = restaurantId;
        SupplyRequestId = supplyRequestId;
        DocumentNumber = documentNumber;
        Status = SupplyStatus.Prepared;
        PreparedBy = preparedBy;
        PreparedAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public Guid? SupplyRequestId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public SupplyStatus Status { get; private set; }
    public Guid? PreparedBy { get; private set; }
    public DateTimeOffset? PreparedAt { get; private set; }
    public Guid? DispatchedBy { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public Guid? ConfirmedBy { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Dispatch(Guid dispatchedBy)
    {
        RequireStatus(SupplyStatus.Prepared);
        Status = SupplyStatus.Dispatched;
        DispatchedBy = dispatchedBy;
        DispatchedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        RequireStatus(SupplyStatus.Prepared);
        Status = SupplyStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm(Guid confirmedBy, SupplyStatus outcome)
    {
        RequireStatus(SupplyStatus.Dispatched);
        if (outcome is not (SupplyStatus.Confirmed or SupplyStatus.ConfirmedWithDiscrepancy or SupplyStatus.RejectedAtDelivery))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Confirmation outcome must be Confirmed, ConfirmedWithDiscrepancy, or RejectedAtDelivery.");
        }

        Status = outcome;
        ConfirmedBy = confirmedBy;
        ConfirmedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void RequireStatus(SupplyStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Supply {Id} is {Status}, expected {expected}.");
        }
    }
}
