using System.Text.Json;
using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Microsoft.AspNetCore.Http.Json;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateItemRequest(string NameArabic, Guid CategoryId, Guid BaseUnitId, Guid? PurchaseUnitId, Guid? DefaultSupplierId, string? Description);
public sealed record UpdateItemRequest(string NameArabic, Guid CategoryId, Guid? PurchaseUnitId, Guid? DefaultSupplierId, string? Description);
public sealed record ChangeItemBaseUnitRequest(Guid BaseUnitId);

/// <summary>Task 5.10. <c>generatedCode</c> is rejected explicitly with
/// <c>400 GENERATED_FIELD_NOT_ACCEPTED</c> (docs/28 §5.1 point 2) rather than merely absent from
/// the binding DTO (point 1's baseline, which every other Phase 5 resource relies on alone) -
/// the two are not the same control: silently dropping an unknown JSON property would leave an
/// integration bug invisible, so create/update read the raw body first to detect it before
/// binding.</summary>
public static class ItemsEndpoints
{
    public static IEndpointRouteBuilder MapItemsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder items = app.MapGroup("/api/v1/items");

        items.MapPost("/", CreateAsync).RequireAuthorization("items:create").AddEndpointFilter<AntiforgeryEndpointFilter>();
        items.MapGet("/", ListAsync).RequireAuthorization("items:view");
        items.MapGet("/{id:guid}", GetAsync).RequireAuthorization("items:view");
        items.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("items:update").AddEndpointFilter<AntiforgeryEndpointFilter>();
        items.MapPut("/{id:guid}/base-unit", ChangeBaseUnitAsync).RequireAuthorization("items:update").AddEndpointFilter<AntiforgeryEndpointFilter>();
        items.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization("items:delete").AddEndpointFilter<AntiforgeryEndpointFilter>();
        items.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("items:delete").AddEndpointFilter<AntiforgeryEndpointFilter>();
        items.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("items:delete").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(HttpContext httpContext, IItemService service)
    {
        (CreateItemRequest? request, IResult? rejection) = await ReadBodyRejectingGeneratedFieldsAsync<CreateItemRequest>(httpContext, "generatedCode");
        if (rejection is not null)
        {
            return rejection;
        }

        var command = new CreateItemCommand(
            request!.NameArabic, request.CategoryId, request.BaseUnitId, request.PurchaseUnitId, request.DefaultSupplierId, request.Description);
        var result = await service.CreateAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/items/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IItemService service, int? limit, string? cursor, string? q)
    {
        KeysetPage<ItemSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, q, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IItemService service)
    {
        ItemSummary? item = await service.GetAsync(id, httpContext.RequestAborted);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> UpdateAsync(Guid id, HttpContext httpContext, IItemService service)
    {
        (UpdateItemRequest? request, IResult? rejection) = await ReadBodyRejectingGeneratedFieldsAsync<UpdateItemRequest>(httpContext, "generatedCode");
        if (rejection is not null)
        {
            return rejection;
        }

        var command = new UpdateItemCommand(request!.NameArabic, request.CategoryId, request.PurchaseUnitId, request.DefaultSupplierId, request.Description);
        var result = await service.UpdateAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ChangeBaseUnitAsync(Guid id, ChangeItemBaseUnitRequest request, HttpContext httpContext, IItemService service)
    {
        var result = await service.ChangeBaseUnitAsync(id, request.BaseUnitId, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext httpContext, IItemService service)
    {
        var result = await service.DeleteAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.NoContent() : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeactivateAsync(Guid id, HttpContext httpContext, IItemService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, HttpContext httpContext, IItemService service)
    {
        var result = await service.ReactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<(T? Request, IResult? Rejection)> ReadBodyRejectingGeneratedFieldsAsync<T>(HttpContext httpContext, params string[] forbiddenProperties)
    {
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);

        foreach (string property in forbiddenProperties)
        {
            if (document.RootElement.TryGetProperty(property, out _))
            {
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.GeneratedFieldNotAccepted);
                return (default, Results.Empty);
            }
        }

        JsonSerializerOptions serializerOptions = httpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<JsonOptions>>().Value.SerializerOptions;
        T request = document.RootElement.Deserialize<T>(serializerOptions)!;
        return (request, null);
    }
}
