using Inventory.Application.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Authorization;

/// <summary>See <see cref="IPermissionEvaluator"/>. Extracted from
/// <c>Inventory.Infrastructure.Identity.AccountProfileReader</c> (Phase 3), which now delegates
/// here too, so <c>GET /account/me</c> and the authorization pipeline's permission checks can
/// never compute two different answers for the same user.</summary>
public sealed class PermissionEvaluator : IPermissionEvaluator
{
    private readonly InventoryDbContext _context;

    public PermissionEvaluator(InventoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        List<string> roleCodes = await _context.RolePermissions
            .Where(rp => _context.UserRoles.Any(ur => ur.UserId == userId && ur.RoleId == rp.RoleId))
            .Join(_context.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .ToListAsync(cancellationToken);

        var userGrants = await _context.UserPermissions
            .Where(up => up.UserId == userId)
            .Join(_context.Permissions, up => up.PermissionId, p => p.Id, (up, p) => new { p.Code, up.IsGranted })
            .ToListAsync(cancellationToken);

        var effective = new HashSet<string>(roleCodes, StringComparer.Ordinal);
        foreach (var grant in userGrants)
        {
            if (grant.IsGranted)
            {
                effective.Add(grant.Code);
            }
            else
            {
                effective.Remove(grant.Code);
            }
        }

        return effective;
    }
}
