using Inventory.Application.Common;

namespace Inventory.Api.Errors;

/// <summary>Maps <see cref="TransactionalError"/> to an HTTP status and docs/13 error code,
/// shared by every Phase 8+ transactional-workflow endpoint (receiving today; supply,
/// confirmation, and stock counts follow the identical shape).</summary>
internal static class TransactionalErrorWriter
{
    /// <summary><paramref name="errorDetail"/> - when the caller set
    /// <c>TransactionalResult.ErrorDetail</c> to an actual docs/13 catalogue code (e.g.
    /// <c>CONVERSION_NOT_DEFINED</c>, <c>WAREHOUSE_UNAVAILABLE</c>) - overrides the
    /// generic per-category code below. A detail that is merely descriptive text and not a real
    /// catalogue code must not be passed here.</summary>
    public static async Task<IResult> WriteErrorAsync(HttpContext httpContext, TransactionalError error, string? errorDetail = null)
    {
        if (error == TransactionalError.NotFound)
        {
            return Results.NotFound();
        }

        (int statusCode, string defaultCode) = error switch
        {
            TransactionalError.InvalidStateTransition => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidStateTransition),
            TransactionalError.InsufficientStock => (StatusCodes.Status400BadRequest, ErrorCodes.InsufficientStock),
            TransactionalError.EmptyDocument => (StatusCodes.Status400BadRequest, ErrorCodes.EmptyDocument),
            TransactionalError.ConcurrencyConflict => (StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict),
            TransactionalError.WarehouseUnavailable => (StatusCodes.Status409Conflict, ErrorCodes.WarehouseUnavailable),
            TransactionalError.AlreadyConfirmed => (StatusCodes.Status409Conflict, ErrorCodes.AlreadyConfirmed),
            _ => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidStateTransition),
        };

        await ProblemResponseWriter.WriteAsync(httpContext, statusCode, errorDetail ?? defaultCode);
        return Results.Empty;
    }
}
