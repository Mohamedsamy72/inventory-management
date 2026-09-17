using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateSupplierRequest(string NameArabic, string? Phone, string? ContactPerson, string? Address, string? Notes);
public sealed record UpdateSupplierRequest(string NameArabic, string? Phone, string? ContactPerson, string? Address, string? Notes);

/// <summary>Task 5.7.</summary>
public static class SuppliersEndpoints
{
    public static IEndpointRouteBuilder MapSuppliersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder suppliers = app.MapGroup("/api/v1/suppliers");

        suppliers.MapPost("/", CreateAsync).RequireAuthorization("suppliers:manage");
        suppliers.MapGet("/", ListAsync).RequireAuthorization("suppliers:manage");
        suppliers.MapGet("/{id:guid}", GetAsync).RequireAuthorization("suppliers:manage");
        suppliers.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("suppliers:manage");
        suppliers.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("suppliers:manage");
        suppliers.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("suppliers:manage");

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateSupplierRequest request, HttpContext httpContext, ISupplierService service)
    {
        var command = new CreateSupplierCommand(request.NameArabic, request.Phone, request.ContactPerson, request.Address, request.Notes);
        var result = await service.CreateAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/suppliers/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, ISupplierService service, int? limit, string? cursor)
    {
        KeysetPage<SupplierSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, ISupplierService service)
    {
        SupplierSummary? supplier = await service.GetAsync(id, httpContext.RequestAborted);
        return supplier is null ? Results.NotFound() : Results.Ok(supplier);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateSupplierRequest request, HttpContext httpContext, ISupplierService service)
    {
        var command = new UpdateSupplierCommand(request.NameArabic, request.Phone, request.ContactPerson, request.Address, request.Notes);
        var result = await service.UpdateAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeactivateAsync(Guid id, HttpContext httpContext, ISupplierService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, HttpContext httpContext, ISupplierService service)
    {
        var result = await service.ReactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }
}
