using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateWarehouseRequest(string NameArabic, string Code, string? Address, string? Description);
public sealed record UpdateWarehouseRequest(string NameArabic, string? Address, string? Description);
public sealed record WarehouseNameOption(Guid Id, string NameArabic, string Code);

/// <summary>Task 5.8.</summary>
public static class WarehousesEndpoints
{
    public static IEndpointRouteBuilder MapWarehousesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder warehouses = app.MapGroup("/api/v1/warehouses");

        warehouses.MapPost("/", CreateAsync).RequireAuthorization("warehouses:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        warehouses.MapGet("/", ListAsync).RequireAuthorization("warehouses:manage");
        // Any authenticated tenant user (no specific permission) - id/name/code only, never
        // address/description. Warehouse Staff and Restaurant Supervisor hold no warehouses:*
        // permission at all (docs/03 §"Warehouses & Branches" - both ❌), yet every screen they
        // use (receiving, supply requests, supplies) references a warehouseId they need to show
        // as a name, not a bare GUID. The full manage-gated list stays manage-only; this is
        // deliberately a much smaller, non-sensitive projection of the same tenant-scoped data.
        warehouses.MapGet("/names", ListNamesAsync).RequireAuthorization();
        warehouses.MapGet("/{id:guid}", GetAsync).RequireAuthorization("warehouses:manage");
        warehouses.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("warehouses:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        warehouses.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("warehouses:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        warehouses.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("warehouses:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateWarehouseRequest request, HttpContext httpContext, IWarehouseService service)
    {
        var command = new CreateWarehouseCommand(request.NameArabic, request.Code, request.Address, request.Description);
        var result = await service.CreateAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/warehouses/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IWarehouseService service, int? limit, string? cursor)
    {
        KeysetPage<WarehouseSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> ListNamesAsync(HttpContext httpContext, IWarehouseService service)
    {
        KeysetPage<WarehouseSummary> page = await service.ListAsync(200, null, httpContext.RequestAborted);
        return Results.Ok(page.Items.Select(w => new WarehouseNameOption(w.Id, w.NameArabic, w.Code)).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IWarehouseService service)
    {
        WarehouseSummary? warehouse = await service.GetAsync(id, httpContext.RequestAborted);
        return warehouse is null ? Results.NotFound() : Results.Ok(warehouse);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateWarehouseRequest request, HttpContext httpContext, IWarehouseService service)
    {
        var command = new UpdateWarehouseCommand(request.NameArabic, request.Address, request.Description);
        var result = await service.UpdateAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeactivateAsync(Guid id, HttpContext httpContext, IWarehouseService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, HttpContext httpContext, IWarehouseService service)
    {
        var result = await service.ReactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }
}
