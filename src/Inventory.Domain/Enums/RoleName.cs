namespace Inventory.Domain.Enums;

/// <summary>
/// The exactly-five product roles (docs/03). ADR-014: a user holds exactly one. `Accountant` is
/// permanently retired (ADR-005) and must never be added here.
/// </summary>
public enum RoleName
{
    Owner,
    Admin,
    WarehouseStaff,
    RestaurantSupervisor,
    User,
}
