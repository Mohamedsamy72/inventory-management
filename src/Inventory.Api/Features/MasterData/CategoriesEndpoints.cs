using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.MasterData;

namespace Inventory.Api.Features.MasterData;

public sealed record CreateCategoryRequest(string NameArabic, string? Description);
public sealed record UpdateCategoryRequest(string NameArabic, string? Description);

/// <summary>Task 5.5.</summary>
public static class CategoriesEndpoints
{
    public static IEndpointRouteBuilder MapCategoriesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder categories = app.MapGroup("/api/v1/categories");

        // docs/03 §3: categories:manage is the ONLY category permission - there is no separate
        // "view" tier (Warehouse Staff/Restaurant Supervisor get no category access by default,
        // matching their ❌ row in the matrix), so every operation here gates on it alike.
        categories.MapPost("/", CreateAsync).RequireAuthorization("categories:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        categories.MapGet("/", ListAsync).RequireAuthorization("categories:manage");
        categories.MapGet("/{id:guid}", GetAsync).RequireAuthorization("categories:manage");
        categories.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization("categories:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        categories.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization("categories:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        categories.MapPost("/{id:guid}/reactivate", ReactivateAsync).RequireAuthorization("categories:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateCategoryRequest request, HttpContext httpContext, ICategoryService service)
    {
        var result = await service.CreateAsync(new CreateCategoryCommand(request.NameArabic, request.Description), httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/categories/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext, ICategoryService service, int? limit, string? cursor)
    {
        KeysetPage<CategorySummary> page = await service.ListAsync(
            KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, ICategoryService service)
    {
        CategorySummary? category = await service.GetAsync(id, httpContext.RequestAborted);
        return category is null ? Results.NotFound() : Results.Ok(category);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateCategoryRequest request, HttpContext httpContext, ICategoryService service)
    {
        var result = await service.UpdateAsync(id, new UpdateCategoryCommand(request.NameArabic, request.Description), httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> DeactivateAsync(Guid id, HttpContext httpContext, ICategoryService service)
    {
        var result = await service.DeactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private static async Task<IResult> ReactivateAsync(Guid id, HttpContext httpContext, ICategoryService service)
    {
        var result = await service.ReactivateAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }
}
