using Inventory.Domain.Enums;

namespace Inventory.Application.Users;

public sealed record CreateUserCommand(string FullName, string MobileNumber, string Password, RoleName Role);

public sealed record UserSummary(Guid Id, string FullName, string MobileNumber, RoleName? Role, bool IsActive);

public sealed record UserScope(IReadOnlyList<Guid> WarehouseIds, IReadOnlyList<Guid> RestaurantIds);

public sealed record PermissionGrant(string Code, bool IsGranted);

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
