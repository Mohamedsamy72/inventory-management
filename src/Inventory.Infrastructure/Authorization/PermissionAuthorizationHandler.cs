using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Inventory.Infrastructure.Authorization;

/// <summary>
/// The pipeline-level stages 3 (role denial) and part of 2 (permission grant) of docs/04 §15 -
/// stage 2's tenant filter itself is the EF global query filter, already applied before any
/// query this handler's caller runs. Deliberately re-checks role denial here, independently of
/// whatever <see cref="IPermissionEvaluator"/> or the grant-time guard (task 4.4) already
/// enforce - docs/09 §4 "Risks": a check implemented only where grants are written would be
/// silently bypassed by any future code path that writes a `user_permissions` row a different
/// way. This handler is the one place every permission-gated endpoint passes through, so it is
/// the one place that cannot be forgotten.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IPermissionEvaluator _permissionEvaluator;

    public PermissionAuthorizationHandler(ICurrentUserService currentUserService, IPermissionEvaluator permissionEvaluator)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _permissionEvaluator = permissionEvaluator ?? throw new ArgumentNullException(nameof(permissionEvaluator));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return;
        }

        // ADR-012: Owner-only, unconditionally, for these four codes - overrides any grant a
        // user_permissions row might otherwise contain, for every role including Owner's own
        // (Owner already carries them via the role baseline, so this never blocks Owner; it
        // exists purely to block everyone else even in the presence of a bad grant row).
        if (!Permission.IsCodeGrantable(requirement.PermissionCode) && _currentUserService.Role != Domain.Enums.RoleName.Owner)
        {
            return;
        }

        // IAuthorizationHandler.HandleRequirementAsync carries no CancellationToken (an ASP.NET
        // Core API limitation, not an oversight here) - matches every built-in handler.
        IReadOnlySet<string> effective = await _permissionEvaluator.GetEffectivePermissionsAsync(
            _currentUserService.UserId, CancellationToken.None);

        if (effective.Contains(requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
