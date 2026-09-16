using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// Header for a supplier delivery. State machine: Draft -&gt; Submitted -&gt; Verified, with
/// Reverse permitted from Submitted or Verified but never from Reversed (docs/04 section 18.3).
/// The actual ledger postings (submit/verify/reverse) are Phase 7-8's IStockPostingService -
/// this entity only guards which transitions are legal.
/// </summary>
public sealed class ReceivingOrder : Entity, ITenantScopedEntity
{
    private ReceivingOrder()
    {
    }

    public ReceivingOrder(Guid companyId, Guid warehouseId, Guid? supplierId, string documentNumber, DateOnly businessDate, Guid createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        WarehouseId = warehouseId;
        SupplierId = supplierId;
        DocumentNumber = documentNumber;
        Status = ReceivingOrderStatus.Draft;
        BusinessDate = businessDate;
        CreatedBy = createdBy;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid? SupplierId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public ReceivingOrderStatus Status { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public Guid? VerifiedBy { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public Guid? ReversedBy { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public string? ReversalReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Submit()
    {
        RequireStatus(ReceivingOrderStatus.Draft);
        Status = ReceivingOrderStatus.Submitted;
        SubmittedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Verify(Guid verifiedBy)
    {
        RequireStatus(ReceivingOrderStatus.Submitted);
        Status = ReceivingOrderStatus.Verified;
        VerifiedBy = verifiedBy;
        VerifiedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reverse(Guid reversedBy, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status is not (ReceivingOrderStatus.Submitted or ReceivingOrderStatus.Verified))
        {
            throw new InvalidOperationException($"ReceivingOrder {Id} cannot be reversed from status {Status}; only Submitted or Verified may be reversed.");
        }

        Status = ReceivingOrderStatus.Reversed;
        ReversedBy = reversedBy;
        ReversedAt = DateTimeOffset.UtcNow;
        ReversalReason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void RequireStatus(ReceivingOrderStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"ReceivingOrder {Id} is {Status}, expected {expected}.");
        }
    }
}
