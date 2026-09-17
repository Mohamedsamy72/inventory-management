using Inventory.Domain.Enums;

namespace Inventory.Application.Auth;

/// <summary>The full response shape for GET /account/me (task 3.12): identity, the single role,
/// scopes, and granted permissions.</summary>
public sealed record AccountProfile(
    Guid UserId,
    Guid CompanyId,
    string FullName,
    string MobileNumber,
    RoleName? Role,
    IReadOnlyList<Guid> WarehouseScopeIds,
    IReadOnlyList<Guid> RestaurantScopeIds,
    IReadOnlyList<string> PermissionCodes);

public interface IAccountProfileReader
{
    Task<AccountProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}
