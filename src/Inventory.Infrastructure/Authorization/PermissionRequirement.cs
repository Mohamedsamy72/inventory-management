using Microsoft.AspNetCore.Authorization;

namespace Inventory.Infrastructure.Authorization;

/// <summary>One `&lt;module&gt;:&lt;action&gt;` permission code (task 4.2, docs/03 §3), used as
/// an ASP.NET Core authorization policy name via <see cref="PermissionPolicyProvider"/>.</summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        PermissionCode = permissionCode;
    }

    public string PermissionCode { get; }
}
