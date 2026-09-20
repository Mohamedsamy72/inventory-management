using Inventory.Domain.Enums;

namespace Inventory.Application.Users;

/// <summary>`Scope` is optional; when supplied it is validated exactly like <c>SetScopeAsync</c> (tenant, role fit) and applied
/// in the same transaction as the account itself.</summary>
public sealed record CreateUserCommand(string FullName, string MobileNumber, string Password, RoleName Role, UserScope? Scope = null);

/// <summary>Owner-only profile edit: name for any manager, mobile number only for the Owner (it is the login identifier).</summary>
public sealed record UpdateUserCommand(string FullName, string MobileNumber);

public sealed record UserSummary(Guid Id, string FullName, string MobileNumber, RoleName? Role, bool IsActive);

public sealed record UserScope(IReadOnlyList<Guid> WarehouseIds, IReadOnlyList<Guid> RestaurantIds);

public sealed record PermissionGrant(string Code, bool IsGranted);

/// <summary>One row of the global permission catalogue (docs/03 §3) - lets the user-management
/// UI render every real permission's Arabic description and grantability instead of a caller
/// having to hardcode the catalogue a second time.</summary>
public sealed record PermissionCatalogueItem(string Code, string Description, string Module, bool IsGrantable);

/// <summary>Why a task 4.14 user-management operation did not succeed. Each maps to exactly one
/// documented error code at the endpoint layer - kept here, not as raw HTTP status codes, so the
/// service stays free of any ASP.NET Core dependency (ADR-002).</summary>
public enum UserManagementError
{
    None,
    NotFound,
    NonGrantablePermission,
    PrivilegeEscalation,
    IdentityCreationFailed,
    /// <summary>Mobile number is not 10-15 digits after normalization.</summary>
    InvalidMobile,
    /// <summary>Mobile number already belongs to another account (unique across ALL companies - it is the login id).</summary>
    DuplicateMobile,
    /// <summary>Role is not a defined, assignable role.</summary>
    InvalidRole,
    /// <summary>A scope id does not exist in the caller's company, or does not fit the user's role.</summary>
    InvalidScope,
    /// <summary>Full name missing or too long.</summary>
    InvalidName,
}

public sealed record UserManagementResult<T>(bool Succeeded, T? Value, UserManagementError Error, string? ErrorDetail = null);

/// <summary>Factory helpers for <see cref="UserManagementResult{T}"/> - kept on a non-generic
/// type (CA1000: a generic type must not declare static members).</summary>
public static class UserManagementResult
{
    public static UserManagementResult<T> Success<T>(T value) => new(true, value, UserManagementError.None);

    public static UserManagementResult<T> Failure<T>(UserManagementError error, string? detail = null) =>
        new(false, default, error, detail);
}
