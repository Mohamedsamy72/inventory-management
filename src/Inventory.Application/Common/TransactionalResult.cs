namespace Inventory.Application.Common;

/// <summary>
/// Why a transactional workflow operation (Phase 8+ - receiving, supply, confirmation, stock
/// counts) did not succeed. A separate shape from <see cref="MasterDataError"/>: these phases
/// share document state machines and stock-posting failure modes, none of which any Phase 5/6
/// master-data resource has.
/// </summary>
public enum TransactionalError
{
    None,
    NotFound,
    InvalidStateTransition,
    InsufficientStock,
    EmptyDocument,
    ConcurrencyConflict,

    /// <summary>ADR-028/CR-092 - a restaurant's default serving warehouse is missing or
    /// inactive. Maps to <c>409 SERVING_WAREHOUSE_UNAVAILABLE</c>; the server never falls back
    /// to another warehouse.</summary>
    ServingWarehouseUnavailable,

    /// <summary>Task 11.9 - a second, non-replayed confirmation of a supply already past
    /// `Dispatched`. Distinct from the generic <see cref="InvalidStateTransition"/> because
    /// docs/13 gives this specific case its own catalogue code, <c>409 ALREADY_CONFIRMED</c>.</summary>
    AlreadyConfirmed,
}

public sealed record TransactionalResult<T>(bool Succeeded, T? Value, TransactionalError Error, string? ErrorDetail = null);

/// <summary>Factory helpers for <see cref="TransactionalResult{T}"/> (CA1000: a generic type must
/// not declare static members).</summary>
public static class TransactionalResult
{
    public static TransactionalResult<T> Success<T>(T value) => new(true, value, TransactionalError.None);

    public static TransactionalResult<T> Failure<T>(TransactionalError error, string? detail = null) =>
        new(false, default, error, detail);
}

/// <summary>Carries what a Phase 8+ transactional-workflow service needs to call
/// <see cref="IIdempotencyService.RecordResponse"/> inside its own business transaction (docs/30
/// §6.2 step 6). <see cref="RawRequestBody"/> must be the literal bytes of the HTTP request body -
/// <see cref="IIdempotencyService"/> hashes it exactly as received, so a re-serialized DTO would
/// never match the hash a future identical retry computes from the wire.</summary>
public sealed record IdempotencyContext(string Key, string Endpoint, string RawRequestBody);
