using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateRestaurantRequest(string NameArabic, string Code, string? Address, string? Description);
public sealed record UpdateRestaurantRequest(string NameArabic, string? Address, string? Description);
public sealed record RestaurantNameOption(Guid Id, string NameArabic, string Code);
public sealed record SetAllowedWarehousesRequest(IReadOnlyList<Guid> WarehouseIds);

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
        // Read: any authenticated user, scope-checked in the handler (a Restaurant Supervisor only for their own
        // restaurants) - the supply-request form needs it without any restaurants:* permission.
        restaurants.MapGet("/{id:guid}/warehouses", GetAllowedWarehousesAsync).RequireAuthorization();
        restaurants.MapPut("/{id:guid}/warehouses", SetAllowedWarehousesAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        restaurants.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        restaurants.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("restaurants:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateRestaurantRequest request, HttpContext httpContext, IRestaurantService service)
    {
        var command = new CreateRestaurantCommand(request.NameArabic, request.Code, request.Address, request.Description);
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

    private static async Task<IResult> GetAllowedWarehousesAsync(Guid id, HttpContext httpContext, IRestaurantService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        if (currentUser.Role == RoleName.RestaurantSupervisor)
        {
            IReadOnlySet<Guid> authorized = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
            if (!authorized.Contains(id))
            {
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
                return Results.Empty;
            }
        }
        else if (currentUser.Role is not (RoleName.Owner or RoleName.Admin))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
            return Results.Empty;
        }

        IReadOnlyList<AllowedWarehouse>? allowed = await service.GetAllowedWarehousesAsync(id, httpContext.RequestAborted);
        return allowed is null ? Results.NotFound() : Results.Ok(allowed);
    }

    private static async Task<IResult> SetAllowedWarehousesAsync(Guid id, SetAllowedWarehousesRequest request, HttpContext httpContext, IRestaurantService service)
    {
        var result = await service.SetAllowedWarehousesAsync(id, request.WarehouseIds, httpContext.RequestAborted);
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
