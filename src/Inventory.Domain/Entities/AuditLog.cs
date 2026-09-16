using Inventory.Domain.Common;
using Inventory.Domain.Enums;

namespace Inventory.Domain.Entities;

/// <summary>
/// One append-only audit row, written inside the same database transaction as the business
/// change it records (docs/06 section 6.10). Fully immutable after construction - no setter
/// exists - backed by the same database-level append-only enforcement as
/// <see cref="StockLedgerEntry"/> (docs/29 section 4.4). <see cref="OldValues"/> and
/// <see cref="NewValues"/> are pre-sanitised JSON text supplied by the caller: passwords, OTPs,
/// session tokens and connection strings must never reach this constructor in the first place
/// (docs/06 section 6.10) - sanitisation is the writer's responsibility, not this entity's.
/// </summary>
public sealed class AuditLog : Entity, ITenantScopedEntity
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid companyId,
        Guid actorUserId,
        string actorRole,
        string action,
        string entityType,
        Guid entityId,
        string descriptionArabic,
        string? oldValuesJson,
        string? newValuesJson,
        AuditResult result,
        Guid? warehouseId,
        Guid? restaurantId,
        string correlationId,
        string? ipAddress,
        string? userAgent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptionArabic);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        DescriptionArabic = descriptionArabic;
        OldValuesJson = oldValuesJson;
        NewValuesJson = newValuesJson;
        Result = result;
        WarehouseId = warehouseId;
        RestaurantId = restaurantId;
        CorrelationId = correlationId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid CompanyId { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string ActorRole { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string DescriptionArabic { get; private set; } = string.Empty;
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }
    public AuditResult Result { get; private set; }
    public Guid? WarehouseId { get; private set; }
    public Guid? RestaurantId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
