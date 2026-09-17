using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.Discrepancies;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.Discrepancies;

public sealed record ResolveDiscrepancyRequest(string Reason);

/// <summary>Task 12.3: <see cref="IScopeGuard"/>'s fifth consumer - Warehouse Staff sees only
/// their warehouses' variances (every type is warehouse-attributed except a pure receipt
/// variance with no warehouse - which cannot happen, since `SupplyReceiptVariance` always
/// carries the confirming supply's warehouse too); Restaurant Supervisor sees only their
/// restaurants' `SupplyReceiptVariance` rows specifically (docs/03: "review receipt
/// discrepancies" - not receiving or stock-count variances, which have no restaurant side at
/// all). Owner/Admin are unrestricted within the tenant.</summary>
public static class DiscrepanciesEndpoints
{
    public static IEndpointRouteBuilder MapDiscrepanciesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder discrepancies = app.MapGroup("/api/v1/discrepancies");

        discrepancies.MapGet("/", ListAsync).RequireAuthorization("discrepancies:view");
        discrepancies.MapGet("/{id:guid}", GetAsync).RequireAuthorization("discrepancies:view");
        discrepancies.MapPost("/{id:guid}/resolve", ResolveAsync)
            .RequireAuthorization("discrepancies:resolve")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IDiscrepancyService service, IScopeGuard scopeGuard, ICurrentUserService currentUser, int? limit, string? cursor)
    {
        IReadOnlySet<Guid>? warehouseIds = null;
        IReadOnlySet<Guid>? restaurantIds = null;
        DiscrepancyType? type = null;

        switch (currentUser.Role)
        {
            case RoleName.WarehouseStaff:
                warehouseIds = await scopeGuard.GetAuthorizedWarehouseIdsAsync(currentUser.UserId, httpContext.RequestAborted);
                restaurantIds = new HashSet<Guid>();
                break;
            case RoleName.RestaurantSupervisor:
                restaurantIds = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
                warehouseIds = new HashSet<Guid>();
                type = DiscrepancyType.SupplyReceiptVariance;
                break;
        }

        KeysetPage<DiscrepancySummary> page = await service.ListAsync(
            KeysetPagination.ClampLimit(limit), cursor, warehouseIds, restaurantIds, type, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IDiscrepancyService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        DiscrepancySummary? discrepancy = await service.GetAsync(id, httpContext.RequestAborted);
        if (discrepancy is null)
        {
            return Results.NotFound();
        }

        IResult? forbidden = await CheckScopeAsync(discrepancy, httpContext, scopeGuard, currentUser);
        return forbidden ?? Results.Ok(discrepancy);
    }

    private static async Task<IResult> ResolveAsync(Guid id, ResolveDiscrepancyRequest request, HttpContext httpContext, IDiscrepancyService service)
    {
        var command = new ResolveDiscrepancyCommand(request.Reason);
        var result = await service.ResolveAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult?> CheckScopeAsync(DiscrepancySummary discrepancy, HttpContext httpContext, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        bool forbidden = false;

        switch (currentUser.Role)
        {
            case RoleName.WarehouseStaff:
                IReadOnlySet<Guid> warehouseIds = await scopeGuard.GetAuthorizedWarehouseIdsAsync(currentUser.UserId, httpContext.RequestAborted);
                forbidden = discrepancy.WarehouseId is null || !warehouseIds.Contains(discrepancy.WarehouseId.Value);
                break;
            case RoleName.RestaurantSupervisor:
                if (discrepancy.Type != DiscrepancyType.SupplyReceiptVariance)
                {
                    forbidden = true;
                    break;
                }

                IReadOnlySet<Guid> restaurantIds = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
                forbidden = discrepancy.RestaurantId is null || !restaurantIds.Contains(discrepancy.RestaurantId.Value);
                break;
        }

        if (!forbidden)
        {
            return null;
        }

        await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
        return Results.Empty;
    }
}
