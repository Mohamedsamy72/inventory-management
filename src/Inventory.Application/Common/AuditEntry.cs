using Inventory.Domain.Enums;

namespace Inventory.Application.Common;

/// <summary>
/// What a caller hands <see cref="IAuditLogger"/> to record one event (docs/14 §1-2). Carries
/// raw, not-yet-sanitized <paramref name="OldValues"/>/<paramref name="NewValues"/> objects (or
/// null) - <see cref="IAuditLogger"/> itself is responsible for serializing and stripping secret
/// fields (task 4.12), so no caller can forget to.
/// </summary>
public sealed record AuditEntry(
    string Action,
    string EntityType,
    Guid EntityId,
    string DescriptionArabic,
    object? OldValues,
    object? NewValues,
    AuditResult Result,
    Guid? WarehouseId = null,
    Guid? RestaurantId = null);
