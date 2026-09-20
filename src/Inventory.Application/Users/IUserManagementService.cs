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

    /// <summary>The user's current explicit warehouse/restaurant scope - needed to populate a
    /// scope-editing form with what is actually assigned today, not a blank slate.</summary>
    Task<UserManagementResult<UserScope>> GetScopeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Account-state control (never a hard delete, matching every other entity in this
    /// codebase). Mirrors task 4.8's self-modification guard: the caller may not deactivate
    /// their own account.</summary>
    Task<UserManagementResult<UserSummary>> DeactivateAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserManagementResult<UserSummary>> ReactivateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The full global permission catalogue, for a permission-editing UI to render real
    /// codes/descriptions/grantability rather than a client-side hardcoded duplicate.</summary>
    /// <summary>Edits name (any manager) and mobile number (Owner only, never an Owner's own or another
    /// Owner's - those go through OTP). Rotates the security stamp when the mobile changes.</summary>
    /// <summary>The target's EFFECTIVE permission codes (role baseline + explicit grants - explicit denials) -
    /// the permissions editor must start from this, never from an empty set.</summary>
    Task<UserManagementResult<IReadOnlyList<string>>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserManagementResult<UserSummary>> UpdateUserAsync(Guid userId, UpdateUserCommand command, CancellationToken cancellationToken);

    /// <summary>Owner-only administrative password reset for a NON-Owner user: no current password
    /// needed, Identity policy enforced, security stamp rotated (existing sessions die), audited
    /// without any secret. Never usable on an Owner (including the caller) - see the OTP flow.</summary>
    Task<UserManagementResult<bool>> SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionCatalogueItem>> ListPermissionCatalogueAsync(CancellationToken cancellationToken);
}
