using Inventory.Application.Auth;
using Inventory.Application.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Identity;

public sealed class AccountProfileReader : IAccountProfileReader
{
    private readonly InventoryDbContext _context;
    private readonly IPermissionEvaluator _permissionEvaluator;

    public AccountProfileReader(InventoryDbContext context, IPermissionEvaluator permissionEvaluator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _permissionEvaluator = permissionEvaluator ?? throw new ArgumentNullException(nameof(permissionEvaluator));
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

        // Role baseline plus explicit per-user grants, minus explicit per-user denials
        // (IPermissionEvaluator, shared with the Phase 4 authorization pipeline so the two can
        // never compute two different answers for the same user). ADR-012 Role Denial (Admin
        // can never see costs/valuation/audit regardless of any grant) is the pipeline's job,
        // not this read-only projection - this returns the raw effective set either way.
        IReadOnlySet<string> effective = await _permissionEvaluator.GetEffectivePermissionsAsync(userId, cancellationToken);

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
