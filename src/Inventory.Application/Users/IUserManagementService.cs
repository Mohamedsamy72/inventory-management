using Inventory.Domain.Enums;

namespace Inventory.Application.Users;

/// <summary>
/// Task 4.14 - user provisioning, role assignment, scope assignment, and permission overrides,
/// with the privilege-escalation guards of task 4.8 and the non-grantable rejection of task 4.4
/// enforced inside the service itself (not merely at the endpoint), so no future caller of this
/// service can accidentally bypass them. Every mutating method audits its own change (task 4.11)
/// inside the same unit of work as the change itself.
/// </summary>
public interface IUserManagementService
{
    Task<UserManagementResult<UserSummary>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken);

    Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken cancellationToken);

    /// <summary>Enforces ADR-014 (exactly one role) by replacing the existing assignment, and
    /// task 4.8's escalation guards: the caller may not change their own role, and only an
    /// Owner may assign the Owner role.</summary>
    Task<UserManagementResult<UserSummary>> ChangeRoleAsync(Guid userId, RoleName newRole, CancellationToken cancellationToken);

    /// <summary>Replaces the user's entire warehouse/restaurant scope in one call. Task 4.8: the
    /// caller may not change their own scope.</summary>
    Task<UserManagementResult<UserScope>> SetScopeAsync(Guid userId, UserScope scope, CancellationToken cancellationToken);

    /// <summary>Task 4.4: a grant naming a non-grantable code (ADR-012) fails the whole call.
    /// Task 4.8: a non-Owner caller may not grant a code outside their own effective permission
    /// set.</summary>
    Task<UserManagementResult<IReadOnlyList<string>>> SetPermissionsAsync(
        Guid userId, IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken);
}
