using System.Security.Claims;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Identity;

/// <summary>
/// Adds the "company_id" and "user_id" claims <see cref="CurrentUserService"/> reads - Identity
/// has no concept of a tenant, so the default claims factory does not (and cannot) know to add
/// one. Also adds the user's single role (ADR-014) as a standard role claim, so
/// <c>[Authorize(Roles = ...)]</c> works without a second lookup.
/// </summary>
public sealed class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
{
    private readonly InventoryDbContext _context;

    public AppUserClaimsPrincipalFactory(
        UserManager<User> userManager,
        IOptions<IdentityOptions> optionsAccessor,
        InventoryDbContext context)
        : base(userManager, optionsAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(CurrentUserService.CompanyIdClaimType, user.CompanyId.ToString()));
        identity.AddClaim(new Claim(CurrentUserService.UserIdClaimType, user.Id.ToString()));

        string? roleName = await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name.ToString())
            .FirstOrDefaultAsync();

        if (roleName is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }

        return identity;
    }
}
