using Inventory.Application.Common;

namespace Inventory.Api.Errors;

/// <summary>Maps <see cref="MasterDataError"/> to an HTTP status and docs/13 error code, shared
/// by every Phase 5 master-data endpoint file (Categories/Units/Suppliers/Warehouses/
/// Restaurants/Items) so the mapping cannot drift between them.</summary>
internal static class MasterDataErrorWriter
{
    public static async Task<IResult> WriteErrorAsync(HttpContext httpContext, MasterDataError error)
    {
        if (error == MasterDataError.NotFound)
        {
            return Results.NotFound();
        }

        (int statusCode, string code) = error switch
        {
            MasterDataError.DuplicateName => (StatusCodes.Status409Conflict, ErrorCodes.DuplicateName),
            MasterDataError.DuplicateItemName => (StatusCodes.Status409Conflict, ErrorCodes.DuplicateItemName),
            MasterDataError.GeneratedFieldNotAccepted => (StatusCodes.Status400BadRequest, ErrorCodes.GeneratedFieldNotAccepted),
            MasterDataError.SequenceExhausted => (StatusCodes.Status409Conflict, ErrorCodes.SequenceExhausted),
            MasterDataError.WarehouseUnavailable => (StatusCodes.Status409Conflict, ErrorCodes.WarehouseUnavailable),
            MasterDataError.BaseUnitImmutable => (StatusCodes.Status409Conflict, ErrorCodes.BaseUnitImmutable),
            MasterDataError.ConversionNotDefined => (StatusCodes.Status409Conflict, ErrorCodes.ConversionNotDefined),
            MasterDataError.InvalidConversionFactor => (StatusCodes.Status400BadRequest, ErrorCodes.InvalidConversionFactor),
            _ => (StatusCodes.Status400BadRequest, ErrorCodes.DuplicateName),
        };

        await ProblemResponseWriter.WriteAsync(httpContext, statusCode, code);
        return Results.Empty;
    }
}
