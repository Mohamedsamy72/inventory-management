using Inventory.Application.Common;
using Inventory.Application.Users;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Users;

/// <summary>See <see cref="IUserManagementService"/>.</summary>
public sealed class UserManagementService : IUserManagementService
{
    private readonly InventoryDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPermissionEvaluator _permissionEvaluator;
    private readonly IAuditLogger _auditLogger;

    public UserManagementService(
        InventoryDbContext context,
        UserManager<User> userManager,
        ICurrentUserService currentUserService,
        IPermissionEvaluator permissionEvaluator,
        IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _permissionEvaluator = permissionEvaluator ?? throw new ArgumentNullException(nameof(permissionEvaluator));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<UserManagementResult<UserSummary>> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Role == RoleName.Owner && _currentUserService.Role != RoleName.Owner)
        {
            // ADR-014/docs/09 §2.4: only an Owner may create another Owner.
            return UserManagementResult.Failure<UserSummary>(UserManagementError.PrivilegeEscalation);
        }

        Role? role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == command.Role, cancellationToken);
        if (role is null)
        {
            return UserManagementResult.Failure<UserSummary>(UserManagementError.NotFound, "Role");
        }

        var user = new User(_currentUserService.CompanyId, command.FullName, command.MobileNumber, "placeholder", "placeholder");

        // UserManager.CreateAsync -> UserStore.CreateAsync saves eagerly (ASP.NET Core Identity
        // owns that call, not this service), so an explicit transaction is the only way to keep
        // "user row + role assignment + audit row" atomic (docs/14 §1) despite the extra
        // SaveChanges Identity performs internally.
        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        IdentityResult createResult = await _userManager.CreateAsync(user, command.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UserManagementResult.Failure<UserSummary>(
                UserManagementError.IdentityCreationFailed,
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        _context.UserRoles.Add(new UserRole(user.Id, role.Id));

        _auditLogger.Record(new AuditEntry(
            "USER_CREATED",
            nameof(User),
            user.Id,
            $"تم إنشاء مستخدم جديد: {user.FullName} بدور {command.Role}",
            OldValues: null,
            NewValues: new { user.FullName, user.MobileNumber, Role = command.Role },
            AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return UserManagementResult.Success<UserSummary>(new UserSummary(user.Id, user.FullName, user.MobileNumber, command.Role, user.IsActive));
    }

    public async Task<UserSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        RoleName? role = await RoleOfAsync(userId, cancellationToken);
        return new UserSummary(user.Id, user.FullName, user.MobileNumber, role, user.IsActive);
    }

    public async Task<IReadOnlyList<UserSummary>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _context.Users.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new { u.Id, u.FullName, u.MobileNumber, u.IsActive })
            .ToListAsync(cancellationToken);

        var roleByUserId = await _context.UserRoles
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToDictionaryAsync(x => x.UserId, x => (RoleName?)x.Name, cancellationToken);

        return users
            .Select(u => new UserSummary(u.Id, u.FullName, u.MobileNumber, roleByUserId.GetValueOrDefault(u.Id), u.IsActive))
            .ToList();
    }

    public async Task<UserManagementResult<UserSummary>> ChangeRoleAsync(Guid userId, RoleName newRole, CancellationToken cancellationToken)
    {
        if (userId == _currentUserService.UserId)
        {
            // Task 4.8: no self role change.
            return UserManagementResult.Failure<UserSummary>(UserManagementError.PrivilegeEscalation);
        }

        if (newRole == RoleName.Owner && _currentUserService.Role != RoleName.Owner)
        {
            return UserManagementResult.Failure<UserSummary>(UserManagementError.PrivilegeEscalation);
        }

        User? user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return UserManagementResult.Failure<UserSummary>(UserManagementError.NotFound);
        }

        Role? role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == newRole, cancellationToken);
        if (role is null)
        {
            return UserManagementResult.Failure<UserSummary>(UserManagementError.NotFound, "Role");
        }

        RoleName? oldRole = await RoleOfAsync(userId, cancellationToken);

        // ADR-014: exactly one role per user, enforced here by replace-not-add, backed by the
        // DB's UNIQUE (user_id) constraint on user_roles as the last line of defense.
        List<UserRole> existing = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);
        _context.UserRoles.RemoveRange(existing);
        _context.UserRoles.Add(new UserRole(userId, role.Id));

        _auditLogger.Record(new AuditEntry(
            "USER_ROLE_CHANGED",
            nameof(User),
            userId,
            $"تم تغيير دور المستخدم {user.FullName} من {oldRole} إلى {newRole}",
            OldValues: new { Role = oldRole },
            NewValues: new { Role = newRole },
            AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);

        return UserManagementResult.Success<UserSummary>(new UserSummary(user.Id, user.FullName, user.MobileNumber, newRole, user.IsActive));
    }

    public async Task<UserManagementResult<UserScope>> SetScopeAsync(Guid userId, UserScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (userId == _currentUserService.UserId)
        {
            // Task 4.8: no self scope change.
            return UserManagementResult.Failure<UserScope>(UserManagementError.PrivilegeEscalation);
        }

        User? user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return UserManagementResult.Failure<UserScope>(UserManagementError.NotFound);
        }

        List<UserWarehouseScope> existingWarehouseScopes =
            await _context.UserWarehouseScopes.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        List<UserRestaurantScope> existingRestaurantScopes =
            await _context.UserRestaurantScopes.Where(s => s.UserId == userId).ToListAsync(cancellationToken);

        _context.UserWarehouseScopes.RemoveRange(existingWarehouseScopes);
        _context.UserRestaurantScopes.RemoveRange(existingRestaurantScopes);

        Guid companyId = _currentUserService.CompanyId;
        foreach (Guid warehouseId in scope.WarehouseIds.Distinct())
        {
            _context.UserWarehouseScopes.Add(new UserWarehouseScope(companyId, userId, warehouseId));
        }

        foreach (Guid restaurantId in scope.RestaurantIds.Distinct())
        {
            _context.UserRestaurantScopes.Add(new UserRestaurantScope(companyId, userId, restaurantId));
        }

        _auditLogger.Record(new AuditEntry(
            "USER_SCOPE_CHANGED",
            nameof(User),
            userId,
            $"تم تحديث نطاق الوصول للمستخدم {user.FullName}",
            OldValues: new
            {
                WarehouseIds = existingWarehouseScopes.Select(s => s.WarehouseId),
                RestaurantIds = existingRestaurantScopes.Select(s => s.RestaurantId),
            },
            NewValues: new { scope.WarehouseIds, scope.RestaurantIds },
            AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);

        return UserManagementResult.Success<UserScope>(scope);
    }

    public async Task<UserManagementResult<IReadOnlyList<string>>> SetPermissionsAsync(
        Guid userId, IReadOnlyList<PermissionGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);

        User? user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return UserManagementResult.Failure<IReadOnlyList<string>>(UserManagementError.NotFound);
        }

        List<Permission> permissions = await _context.Permissions
            .Where(p => grants.Select(g => g.Code).Contains(p.Code))
            .ToListAsync(cancellationToken);

        var permissionByCode = permissions.ToDictionary(p => p.Code, StringComparer.Ordinal);

        IReadOnlySet<string> actorEffectivePermissions = _currentUserService.Role == RoleName.Owner
            ? new HashSet<string>()
            : await _permissionEvaluator.GetEffectivePermissionsAsync(_currentUserService.UserId, cancellationToken);

        foreach (PermissionGrant grant in grants)
        {
            if (!permissionByCode.TryGetValue(grant.Code, out Permission? permission) || (grant.IsGranted && !permission.IsGrantable))
            {
                // Task 4.4: unknown or non-grantable code fails the whole call - nothing is
                // partially applied.
                return UserManagementResult.Failure<IReadOnlyList<string>>(UserManagementError.NonGrantablePermission, grant.Code);
            }

            if (grant.IsGranted && _currentUserService.Role != RoleName.Owner && !actorEffectivePermissions.Contains(grant.Code))
            {
                // Task 4.8: a non-Owner cannot grant a code they do not themselves hold.
                return UserManagementResult.Failure<IReadOnlyList<string>>(UserManagementError.PrivilegeEscalation, grant.Code);
            }
        }

        var appliedCodes = new List<string>();
        foreach (PermissionGrant grant in grants)
        {
            Permission permission = permissionByCode[grant.Code];

            UserPermission? existing = await _context.UserPermissions
                .FirstOrDefaultAsync(up => up.UserId == userId && up.PermissionId == permission.Id, cancellationToken);

            if (existing is not null)
            {
                _context.UserPermissions.Remove(existing);
            }

            _context.UserPermissions.Add(new UserPermission(userId, permission.Id, grant.IsGranted));
            appliedCodes.Add(grant.Code);
        }

        _auditLogger.Record(new AuditEntry(
            "USER_PERMISSIONS_CHANGED",
            nameof(User),
            userId,
            $"تم تحديث صلاحيات المستخدم {user.FullName}",
            OldValues: null,
            NewValues: grants,
            AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);

        return UserManagementResult.Success<IReadOnlyList<string>>(appliedCodes);
    }

    private async Task<RoleName?> RoleOfAsync(Guid userId, CancellationToken cancellationToken) =>
        await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .Cast<RoleName?>()
            .FirstOrDefaultAsync(cancellationToken);
}
