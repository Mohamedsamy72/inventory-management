using Inventory.Application.Common;

namespace Inventory.Application.Audit;

/// <summary>Phase 14 - the read surface over the audit trail Phase 4 already writes to (docs/09
/// tasks 14.1-14.4). Owner-only, no grant path (ADR-013) - enforced by
/// <c>PermissionAuthorizationHandler</c>'s role-denial check on `audit:view`/`audit:export`
/// (task 4.8), not by anything in this interface or its implementation.</summary>
public interface IAuditReaderService
{
    /// <summary>Task 14.1: the full record, filterable by actor/entity type/date range.</summary>
    Task<KeysetPage<AuditLogSummary>> ListAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Task 14.2: the reduced, human-readable Arabic stream.</summary>
    Task<KeysetPage<AuditActivitySummary>> ListActivityAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>Task 14.4: identical data to <see cref="ListAsync"/>, but writes its own audit
    /// entry recording that an export happened - a distinct, gated act (`audit:export`, not
    /// `audit:view`) from merely viewing the trail.</summary>
    Task<KeysetPage<AuditLogSummary>> ExportAsync(AuditLogFilter filter, int limit, string? cursor, CancellationToken cancellationToken);
}
