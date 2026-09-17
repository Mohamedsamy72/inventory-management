using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.Supplies;
using Inventory.Application.SupplyRequests;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.Supplies;

public sealed record FulfillSupplyLineRequest(Guid SupplyRequestItemId, decimal FulfilledQuantity);
public sealed record FulfillSupplyRequestRequest(IReadOnlyList<FulfillSupplyLineRequest> Lines);

/// <summary>Task 10.7: <see cref="IScopeGuard"/> warehouse-scope enforcement for Warehouse Staff
/// on fulfil/dispatch/cancel (docs/03 §Supply Requests and §Supplies rows - both scoped for
/// Warehouse Staff); Owner/Admin are unrestricted within the tenant.</summary>
public static class SuppliesEndpoints
{
    public static IEndpointRouteBuilder MapSuppliesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/supply-requests/{id:guid}/fulfill", FulfillAsync)
            .RequireAuthorization("supply_requests:fulfill")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        RouteGroupBuilder supplies = app.MapGroup("/api/v1/supplies");

        supplies.MapGet("/", ListAsync).RequireAuthorization("supplies:view");
        supplies.MapGet("/{id:guid}", GetAsync).RequireAuthorization("supplies:view");
        supplies.MapPost("/{id:guid}/dispatch", DispatchAsync)
            .RequireAuthorization("supplies:dispatch")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        supplies.MapPost("/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization("supply_requests:fulfill")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> FulfillAsync(Guid id, FulfillSupplyRequestRequest request, HttpContext httpContext, ISupplyService service, ISupplyRequestService supplyRequestService, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        SupplyRequestSummary? supplyRequest = await supplyRequestService.GetAsync(id, httpContext.RequestAborted);
        if (supplyRequest is null)
        {
            return Results.NotFound();
        }

        IResult? forbidden = await CheckWarehouseScopeAsync(supplyRequest.WarehouseId, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new FulfillSupplyRequestCommand(request.Lines.Select(l => new FulfillSupplyLineCommand(l.SupplyRequestItemId, l.FulfilledQuantity)).ToList());
        var result = await service.FulfillAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/supplies/{result.Value!.Supply.Id}", result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, ISupplyService service, int? limit, string? cursor)
    {
        KeysetPage<SupplySummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, ISupplyService service)
    {
        SupplySummary? supply = await service.GetAsync(id, httpContext.RequestAborted);
        return supply is null ? Results.NotFound() : Results.Ok(supply);
    }

    private static async Task<IResult> DispatchAsync(Guid id, HttpContext httpContext, ISupplyService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckSupplyWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.DispatchAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpContext httpContext, ISupplyService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckSupplyWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.CancelAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult?> CheckSupplyWarehouseScopeAsync(Guid supplyId, HttpContext httpContext, ISupplyService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        SupplySummary? supply = await service.GetAsync(supplyId, httpContext.RequestAborted);
        if (supply is null)
        {
            return Results.NotFound();
        }

        return await CheckWarehouseScopeAsync(supply.WarehouseId, httpContext, scopeGuard, currentUser);
    }

    private static async Task<IResult?> CheckWarehouseScopeAsync(Guid warehouseId, HttpContext httpContext, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        if (currentUser.Role != RoleName.WarehouseStaff)
        {
            return null;
        }

        IReadOnlySet<Guid> authorizedWarehouseIds = await scopeGuard.GetAuthorizedWarehouseIdsAsync(currentUser.UserId, httpContext.RequestAborted);
        if (authorizedWarehouseIds.Contains(warehouseId))
        {
            return null;
        }

        await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
        return Results.Empty;
    }
}
