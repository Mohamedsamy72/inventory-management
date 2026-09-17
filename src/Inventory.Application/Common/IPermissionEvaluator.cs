namespace Inventory.Application.Common;

/// <summary>
/// Computes a user's effective permission-code set: the role baseline (<c>role_permissions</c>)
/// unioned with explicit per-user grants and minus explicit per-user denials
/// (<c>user_permissions</c>) - docs/03 §4. Shared by <c>GET /account/me</c> (Phase 3) and the
/// authorization pipeline's permission requirement handler (Phase 4, task 4.2), so the two never
/// drift apart on what "effective" means.
/// </summary>
public interface IPermissionEvaluator
{
    Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken);
}
