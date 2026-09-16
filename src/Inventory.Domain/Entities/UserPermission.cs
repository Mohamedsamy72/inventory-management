namespace Inventory.Domain.Entities;

/// <summary>An explicit per-user permission grant or denial, on top of the role's baseline.</summary>
public sealed class UserPermission
{
    private UserPermission()
    {
    }

    public UserPermission(Guid userId, Guid permissionId, bool isGranted)
    {
        UserId = userId;
        PermissionId = permissionId;
        IsGranted = isGranted;
    }

    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public bool IsGranted { get; private set; }
}
