using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateUnitRequest(string NameArabic, string? Abbreviation);
public sealed record UpdateUnitRequest(string NameArabic, string? Abbreviation);
public sealed record UnitNameOption(Guid Id, string NameArabic);

/// <summary>Task 5.6.</summary>
public static class UnitsEndpoints
{
    public static IEndpointRouteBuilder MapUnitsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder units = app.MapGroup("/api/v1/units");

        units.MapPost("/", CreateAsync).RequireAuthorization("units:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        units.MapGet("/", ListAsync).RequireAuthorization("units:manage");
        // Mirrors Warehouses/Restaurants/Suppliers' "/names" - Warehouse Staff and Restaurant
        // Supervisor hold no units:manage (docs/03 §"Master Data" - ❌ for both), yet the
        // receiving/supply-request line editors both need a unit picker by name.
        units.MapGet("/names", ListNamesAsync).RequireAuthorization();
        units.MapGet("/{id:guid}", GetAsync).RequireAuthorization("units:manage");
        units.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("units:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        units.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("units:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        units.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("units:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateUnitRequest request, HttpContext httpContext, IUnitService service)
    {
        var result = await service.CreateAsync(new CreateUnitCommand(request.NameArabic, request.Abbreviation), httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/units/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IUnitService service, int? limit, string? cursor)
    {
        KeysetPage<UnitSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> ListNamesAsync(HttpContext httpContext, IUnitService service)
    {
        KeysetPage<UnitSummary> page = await service.ListAsync(200, null, httpContext.RequestAborted);
        return Results.Ok(page.Items.Select(u => new UnitNameOption(u.Id, u.NameArabic)).ToList());
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IUnitService service)
    {
        UnitSummary? unit = await service.GetAsync(id, httpContext.RequestAborted);
        return unit is null ? Results.NotFound() : Results.Ok(unit);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateUnitRequest request, HttpContext httpContext, IUnitService service)
    {
        var result = await service.UpdateAsync(id, new UpdateUnitCommand(request.NameArabic, request.Abbreviation), httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeactivateAsync(Guid id, HttpContext httpContext, IUnitService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, HttpContext httpContext, IUnitService service)
    {
        var result = await service.ReactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }
}
