using Inventory.Application.Common;

namespace Inventory.Api.Errors;

/// <summary>Maps <see cref="TransactionalError"/> to an HTTP status and docs/13 error code,
/// shared by every Phase 8+ transactional-workflow endpoint (receiving today; supply,
/// confirmation, and stock counts follow the identical shape).</summary>
internal static class TransactionalErrorWriter
{
    public static async Task<IResult> WriteErrorAsync(HttpContext httpContext, TransactionalError error)
    {
        if (error == TransactionalError.NotFound)
        {
            return Results.NotFound();
        }

        (int statusCode, string code) = error switch
        {
            TransactionalError.InvalidStateTransition => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidStateTransition),
            TransactionalError.InsufficientStock => (StatusCodes.Status400BadRequest, ErrorCodes.InsufficientStock),
            TransactionalError.EmptyDocument => (StatusCodes.Status400BadRequest, ErrorCodes.EmptyDocument),
            TransactionalError.ConcurrencyConflict => (StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict),
            _ => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidStateTransition),
        };

        await ProblemResponseWriter.WriteAsync(httpContext, statusCode, code);
        return Results.Empty;
    }
}
