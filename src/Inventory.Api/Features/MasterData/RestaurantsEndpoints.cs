using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateRestaurantRequest(string NameArabic, string Code, Guid DefaultServingWarehouseId, string? Address, string? Description);
public sealed record UpdateRestaurantRequest(string NameArabic, string? Address, string? Description);
public sealed record ChangeServingWarehouseRequest(Guid WarehouseId);
public sealed record RestaurantNameOption(Guid Id, string NameArabic, string Code);

/// <summary>Task 5.9.</summary>
public static class RestaurantsEndpoints
{
    public static IEndpointRouteBuilder MapRestaurantsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder restaurants = app.MapGroup("/api/v1/restaurants");

        restaurants.MapPost("/", CreateAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        restaurants.MapGet("/", ListAsync).RequireAuthorization("restaurants:manage");
        // Mirrors WarehousesEndpoints' "/names" - Restaurant Supervisor holds no restaurants:*
        // permission (docs/03 §"Warehouses & Branches" - ❌), yet needs to resolve their own
        // scoped restaurantId to a name (e.g. the supply-request creation dialog).
        restaurants.MapGet("/names", ListNamesAsync).RequireAuthorization();
        restaurants.MapGet("/{id:guid}", GetAsync).RequireAuthorization("restaurants:manage");
        restaurants.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        restaurants.MapPut("/{id:guid}/serving-warehouse", ChangeServingWarehouseAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        restaurants.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        restaurants.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateRestaurantRequest request, HttpContext httpContext, IRestaurantService service)
    {
        var command = new CreateRestaurantCommand(request.NameArabic, request.Code, request.DefaultServingWarehouseId, request.Address, request.Description);
        var result = await service.CreateAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/restaurants/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IRestaurantService service, int? limit, string? cursor)
    {
        KeysetPage<RestaurantSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> ListNamesAsync(HttpContext httpContext, IRestaurantService service)
    {
        KeysetPage<RestaurantSummary> page = await service.ListAsync(200, null, httpContext.RequestAborted);
        return Results.Ok(page.Items.Select(r => new RestaurantNameOption(r.Id, r.NameArabic, r.Code)).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IRestaurantService service)
    {
        RestaurantSummary? restaurant = await service.GetAsync(id, httpContext.RequestAborted);
        return restaurant is null ? Results.NotFound() : Results.Ok(restaurant);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateRestaurantRequest request, HttpContext httpContext, IRestaurantService service)
    {
        var command = new UpdateRestaurantCommand(request.NameArabic, request.Address, request.Description);
        var result = await service.UpdateAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ChangeServingWarehouseAsync(Guid id, ChangeServingWarehouseRequest request, HttpContext httpContext, IRestaurantService service)
    {
        var result = await service.ChangeServingWarehouseAsync(id, request.WarehouseId, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeactivateAsync(Guid id, HttpContext httpContext, IRestaurantService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, HttpContext httpContext, IRestaurantService service)
    {
        var result = await service.ReactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }
}
