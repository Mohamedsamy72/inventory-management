namespace Inventory.Application.Common;

/// <summary>
/// Task 4.6 - server-side-only data scope enforcement for Warehouse Staff and Restaurant
/// Supervisor (docs/03 §4). A handler resolves the resource's warehouse/restaurant id from the
/// database (never from client input) and asks whether the current user's scope includes it;
/// docs/09 §2.1's IDOR-prevention pattern is: out-of-tenant → 404 (the tenant query filter
/// already makes the row invisible), in-tenant-but-out-of-scope → 403 via
/// <c>FORBIDDEN_SCOPE</c> (task 4.7) - the distinction matters because a 404 must never confirm
/// that a resource exists in a company the caller cannot see, while a 403 is fine to return once
/// the caller is already known to share the tenant.
/// </summary>
public interface IScopeGuard
{
    /// <summary>The warehouse ids the user may act on. Empty for a role with no warehouse scope
    /// concept - callers decide whether that means "all" (Owner/Admin, unrestricted within the
    /// tenant) or "none" (Warehouse Staff with no scope rows assigned yet) based on role.</summary>
    Task<IReadOnlySet<Guid>> GetAuthorizedWarehouseIdsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The restaurant ids the user may act on. Same "empty means role-dependent" caveat
    /// as <see cref="GetAuthorizedWarehouseIdsAsync"/>.</summary>
    Task<IReadOnlySet<Guid>> GetAuthorizedRestaurantIdsAsync(Guid userId, CancellationToken cancellationToken);
}
