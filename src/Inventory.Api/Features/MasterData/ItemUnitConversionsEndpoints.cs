using Inventory.Api.Errors;
using Inventory.Application.MasterData;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateItemUnitConversionRequest(Guid FromUnitId, decimal ConversionFactor);

/// <summary>Task 6.1. Nested under the item it belongs to - a conversion has no meaning outside
/// the context of one specific item.</summary>
public static class ItemUnitConversionsEndpoints
{
    public static IEndpointRouteBuilder MapItemUnitConversionsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder conversions = app.MapGroup("/api/v1/items/{itemId:guid}/conversions");

        conversions.MapPost("/", CreateAsync).RequireAuthorization("conversions:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        conversions.MapGet("/", ListAsync).RequireAuthorization("conversions:manage");
        conversions.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("conversions:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        Guid itemId, CreateItemUnitConversionRequest request, HttpContext httpContext, IItemUnitConversionService service)
    {
        var command = new CreateItemUnitConversionCommand(itemId, request.FromUnitId, request.ConversionFactor);
        var result = await service.CreateAsync(command, httpContext.RequestAborted);
        return result.Succeeded
            ? Results.Created($"/api/v1/items/{itemId}/conversions/{result.Value!.Id}", result.Value)
            : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(Guid itemId, HttpContext httpContext, IItemUnitConversionService service) =>
        Results.Ok(await service.ListForItemAsync(itemId, httpContext.RequestAborted));

    private static async Task<IResult> DeactivateAsync(Guid itemId, Guid id, HttpContext httpContext, IItemUnitConversionService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }
}
