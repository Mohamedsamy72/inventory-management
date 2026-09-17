using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Identity;

/// <summary>
/// Adapts <see cref="User"/> - a rich domain entity with private setters and its own invariant
/// methods, not an anaemic Identity DTO - to ASP.NET Core Identity's granular store interfaces
/// (task 3.1, docs/08 §1). "UserName" is the mobile number: this product has no email/username
/// concept (docs/08 - mobile + password only). Every mutation delegates to the entity's own
/// methods (<see cref="User.ChangePasswordHash"/>, <see cref="User.RotateSecurityStamp"/>,
/// etc.) rather than reaching around them, so the domain's invariants stay in one place.
/// </summary>
public sealed class UserStore :
    IUserStore<User>,
    IUserPasswordStore<User>,
    IUserSecurityStampStore<User>,
    IUserLockoutStore<User>
{
    private readonly InventoryDbContext _context;

    public UserStore(InventoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.MobileNumber);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The mobile number is set at creation and never renamed through the Identity store.");

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.MobileNumber);

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken)
    {
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// IgnoreQueryFilters for the same structural reason as <see cref="FindByNameAsync"/>: this
    /// is also called mid-authentication-pipeline (SignInManager.ValidateSecurityStampAsync,
    /// via the cookie's OnValidatePrincipal event) to re-hydrate the user BEFORE that principal
    /// has been assigned to HttpContext.User - so ICurrentUserService.CompanyId still reads
    /// Guid.Empty at this exact point, and the tenant filter would incorrectly reject every
    /// lookup, silently invalidating every session on its very next request (reproduced
    /// directly: login succeeds, the following /account/me call finds the cookie, deserializes
    /// a valid authenticated principal, then this query returns nothing and the session is
    /// torn down). Safe to bypass here specifically because the id comes from a
    /// DataProtection-signed, tamper-proof cookie ticket, not a client-supplied value - the
    /// real tenant-scoping is enforced on every subsequent query in the request once
    /// HttpContext.User reflects the now-validated principal.
    /// </summary>
    public Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out Guid id))
        {
            return Task.FromResult<User?>(null);
        }

        return _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <summary>
    /// Looked up by normalized mobile number - Identity's "normalized user name" slot. Login
    /// (and the OTP/forgot-password flow) call this BEFORE the caller is authenticated, so
    /// <see cref="ICurrentUserService"/>.CompanyId is Guid.Empty at this point (fail-closed by
    /// design) - the global tenant query filter would therefore exclude every real user and
    /// login could never succeed. IgnoreQueryFilters is deliberate here: resolving WHICH tenant
    /// a mobile number belongs to is the entire point of this lookup, the same way any
    /// multi-tenant system must look a user up before it knows their tenant. Mobile numbers are
    /// unique only per-company in the schema (docs/06 uq_users_company_mobile) - a global
    /// lookup by number alone is the documented login contract (docs/08 §1 has no
    /// tenant-selector field), not a gap this fixes around.
    /// </summary>
    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        string normalized = MobileNumberNormalizer.Normalize(normalizedUserName);
        return _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.MobileNumber == normalized, cancellationToken);
    }

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        user.ChangePasswordHash(passwordHash);
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.PasswordHash);

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        user.RotateSecurityStamp(stamp);
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(user.SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEndAt);

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.SetLockoutEnd(lockoutEnd);
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.IncrementAccessFailedCount());

    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        user.ResetAccessFailedCount();
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(user.AccessFailedCount);

    /// <summary>Always true: lockout is always active for every user (no per-user opt-out - docs/08 §1).</summary>
    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <summary>
    /// A no-op, not a throw: UserManager.CreateAsync unconditionally calls this as part of its
    /// normal user-creation flow (to turn lockout on for a new user), so throwing here would
    /// break every user creation. Since <see cref="GetLockoutEnabledAsync"/> always answers
    /// true regardless of what is set, silently accepting the call produces the same observable
    /// behaviour as actually storing and honouring the value.
    /// </summary>
    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public void Dispose()
    {
        // InventoryDbContext's lifetime is owned by DI (scoped), not by this store.
    }
}
