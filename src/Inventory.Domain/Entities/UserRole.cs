namespace Inventory.Domain.Entities;

/// <summary>
/// The single role a user holds (ADR-014). Composite key (user_id, role_id) with a UNIQUE
/// (user_id) constraint enforcing "exactly one" at the database layer, not just in application code.
/// </summary>
public sealed class UserRole
{
    private UserRole()
    {
    }

    public UserRole(Guid userId, Guid roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
}
