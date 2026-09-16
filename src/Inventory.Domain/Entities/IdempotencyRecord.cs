using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

/// <summary>
/// Caches the role-projected response of a sensitive mutation so a retried request replays the
/// same result instead of double-posting (docs/30 section 6). The cached payload MUST be the
/// role-projected DTO, never the domain object - an Owner's cached response must never be
/// replayable to a non-Owner (docs/30 section 10) - which is why this is plain JSON text
/// supplied by the caller, not a domain object this entity would have to re-project itself.
/// </summary>
public sealed class IdempotencyRecord : Entity, ITenantScopedEntity
{
    private IdempotencyRecord()
    {
    }

    public IdempotencyRecord(Guid companyId, Guid userId, string idempotencyKey, string endpoint, string requestHash, int responseStatusCode, string? responsePayloadJson, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        UserId = userId;
        IdempotencyKey = idempotencyKey;
        Endpoint = endpoint;
        RequestHash = requestHash;
        ResponseStatusCode = responseStatusCode;
        ResponsePayloadJson = responsePayloadJson;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt;
    }

    public Guid CompanyId { get; private set; }
    public Guid UserId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string Endpoint { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public int ResponseStatusCode { get; private set; }
    public string? ResponsePayloadJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
}
