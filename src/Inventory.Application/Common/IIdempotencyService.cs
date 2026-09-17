namespace Inventory.Application.Common;

/// <summary>
/// Task 7.8 (docs/30 §6.2). Scoped to <c>(company_id, user_id)</c> - a key can never replay
/// another user's operation or cross a tenant (docs/30 §6.3). Callers MUST store only the
/// role-projected response DTO, never the domain object (task 7.10, docs/30 §10) - an Owner's
/// cached response must never be replayable to a non-Owner.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>Steps 1-5 of docs/30 §6.2's protocol: computes the canonical request hash and
    /// looks up <c>(company_id, user_id, idempotencyKey)</c>. Read-only - performs no write.</summary>
    Task<IdempotencyCheckResult> CheckAsync(string idempotencyKey, string endpoint, object requestBody, CancellationToken cancellationToken);

    /// <summary>Adds the response record to the CURRENT unit of work - not saved here. The
    /// caller's own <c>SaveChangesAsync</c>, inside the same transaction as the business change
    /// it is idempotency-guarding, must be what actually persists it (the same "same transaction"
    /// requirement as <see cref="IAuditLogger"/>). Step 6's concurrent-duplicate race (docs/30
    /// §6.2) surfaces here as an ordinary unique-constraint <c>DbUpdateException</c> from that
    /// <c>SaveChangesAsync</c> call if two identical requests raced each other - the caller
    /// catches it, lets its own transaction roll back, and calls <see cref="CheckAsync"/> again
    /// to fetch the winner's now-committed response instead.</summary>
    void RecordResponse(string idempotencyKey, string endpoint, object requestBody, int statusCode, object? responsePayload);

    /// <summary>Task 7.11 - deletes every record past its 24-hour retention (docs/30 §6.3).
    /// Returns the number removed.</summary>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken);
}

public enum IdempotencyOutcome
{
    /// <summary>No record exists for this key - proceed with the business operation.</summary>
    New,

    /// <summary>The same key with the same payload - docs/30 §6.2 step 4: return the cached
    /// status/payload verbatim, perform NO business work.</summary>
    Replay,

    /// <summary>The same key with a DIFFERENT payload - docs/30 §6.2 step 5: `409
    /// IDEMPOTENCY_KEY_REUSE`, never execute.</summary>
    KeyReuse,
}

public sealed record IdempotencyCheckResult(IdempotencyOutcome Outcome, int CachedStatusCode = 0, string? CachedPayloadJson = null);
