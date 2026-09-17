using Inventory.Application.Auth;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Identity;

public sealed class AccountProfileReader : IAccountProfileReader
{
    private readonly InventoryDbContext _context;

    public AccountProfileReader(InventoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        Domain.Enums.RoleName? role = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .Cast<Domain.Enums.RoleName?>()
            .FirstOrDefaultAsync(cancellationToken);

        List<Guid> warehouseScopeIds = await _context.UserWarehouseScopes
            .Where(s => s.UserId == userId)
            .Select(s => s.WarehouseId)
            .ToListAsync(cancellationToken);

        List<Guid> restaurantScopeIds = await _context.UserRestaurantScopes
            .Where(s => s.UserId == userId)
            .Select(s => s.RestaurantId)
            .ToListAsync(cancellationToken);

        // Role baseline plus explicit per-user grants, minus explicit per-user denials. ADR-012
        // Role Denial (Admin can never see costs/valuation/audit regardless of any grant) is
        // Phase 4's IScopeGuard/policy pipeline, not this read-only projection - deliberately
        // not special-cased here yet ("stay in phase", docs/09 §4.3).
        List<string> roleCodes = role is null
            ? []
            : await _context.RolePermissions
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

        return new AccountProfile(
            user.Id,
            user.CompanyId,
            user.FullName,
            user.MobileNumber,
            role,
            warehouseScopeIds,
            restaurantScopeIds,
            effective.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }
}
