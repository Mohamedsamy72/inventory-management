using Inventory.Domain.Enums;

namespace Inventory.Application.Common;

/// <summary>
/// The authenticated principal's identity, as seen by the current request. Tenant identity
/// comes EXCLUSIVELY from here - never from a client-supplied companyId in body, query, route,
/// or header (docs/06 section 6.7). Before Phase 3 wires real authentication, every
/// implementation must return <see cref="CompanyId"/> = <see cref="Guid.Empty"/> rather than a
/// nullable "unknown" value, so the EF global query filter fails closed (matches nothing) by
/// default instead of accidentally matching every tenant.
/// </summary>
public interface ICurrentUserService
{
    Guid CompanyId { get; }
    Guid UserId { get; }
    bool IsAuthenticated { get; }

    /// <summary>The single role claim set at login (ADR-014). Null when unauthenticated or for
    /// a principal predating role assignment - callers must treat that as "no role", never as
    /// an implicit grant.</summary>
    RoleName? Role { get; }
}
