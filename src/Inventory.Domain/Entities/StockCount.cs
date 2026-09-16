using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// A physical stock count. State machine: Draft -&gt; InProgress -&gt; PendingApproval -&gt;
/// Approved | Rejected (docs/04 section 18.4). Only Owner/Admin may approve (Warehouse Staff
/// records counts but never approves - docs/03); that role check is Phase 4/13's job, not this
/// entity's. Approval posts a PHYSICAL_ADJUSTMENT per variance; rejection posts nothing.
/// </summary>
public sealed class StockCount : Entity, ITenantScopedEntity
{
    private StockCount()
    {
    }

    public StockCount(Guid companyId, Guid warehouseId, string documentNumber, bool isBlindCount, Guid openedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        WarehouseId = warehouseId;
        DocumentNumber = documentNumber;
        Status = StockCountStatus.Draft;
        IsBlindCount = isBlindCount;
        OpenedBy = openedBy;
        OpenedAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string DocumentNumber { get; private set; } = string.Empty;
    public StockCountStatus Status { get; private set; }
    public bool IsBlindCount { get; private set; }
    public Guid OpenedBy { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void BeginCounting()
    {
        RequireStatus(StockCountStatus.Draft);
        Status = StockCountStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SubmitForApproval()
    {
        RequireStatus(StockCountStatus.InProgress);
        Status = StockCountStatus.PendingApproval;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Approve(Guid approvedBy)
    {
        RequireStatus(StockCountStatus.PendingApproval);
        Status = StockCountStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject()
    {
        RequireStatus(StockCountStatus.PendingApproval);
        Status = StockCountStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void RequireStatus(StockCountStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"StockCount {Id} is {Status}, expected {expected}.");
        }
    }
}
