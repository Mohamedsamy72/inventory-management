using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.SupplyRequests;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.SupplyRequests;

/// <summary>ADR-028 (docs/09 task 9.8): deliberately carries no <c>WarehouseId</c> - the server
/// derives it from the restaurant's own configuration. A client posting one anyway is simply
/// ignored by the model binder, since no property exists here to bind it to (task 9.12).</summary>
public sealed record CreateSupplyRequestRequest(Guid RestaurantId);
public sealed record AddSupplyRequestLineRequest(Guid ItemId, Guid UnitId, decimal RequestedQuantity, string? Notes);
public sealed record UpdateSupplyRequestLineRequest(Guid UnitId, decimal RequestedQuantity, string? Notes);

/// <summary>Task 9.9: <see cref="IScopeGuard"/> restaurant-scope enforcement for Restaurant
/// Supervisor (docs/03 §Supply Requests row - create/view scoped, no fulfil permission at all);
/// Owner/Admin are unrestricted within the tenant. Warehouse Staff holds `view`/`fulfill` only -
/// no `create` permission - so it never reaches the restaurant-scope check here at all.</summary>
public static class SupplyRequestsEndpoints
{
    public static IEndpointRouteBuilder MapSupplyRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder requests = app.MapGroup("/api/v1/supply-requests");

        requests.MapPost("/", CreateAsync)
            .RequireAuthorization("supply_requests:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        requests.MapGet("/", ListAsync).RequireAuthorization("supply_requests:view");
        requests.MapGet("/{id:guid}", GetAsync).RequireAuthorization("supply_requests:view");
        requests.MapPost("/{id:guid}/items", AddLineAsync)
            .RequireAuthorization("supply_requests:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        requests.MapPut("/{id:guid}/items/{lineId:guid}", UpdateLineAsync)
            .RequireAuthorization("supply_requests:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        requests.MapDelete("/{id:guid}/items/{lineId:guid}", RemoveLineAsync)
            .RequireAuthorization("supply_requests:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        requests.MapPost("/{id:guid}/submit", SubmitAsync)
            .RequireAuthorization("supply_requests:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        requests.MapPost("/{id:guid}/cancel", CancelAsync)
            .RequireAuthorization("supply_requests:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateSupplyRequestRequest request, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRestaurantScopeAsync(request.RestaurantId, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new CreateSupplyRequestCommand(request.RestaurantId);
        var result = await service.CreateDraftAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/supply-requests/{result.Value!.Id}", result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, ISupplyRequestService service, int? limit, string? cursor)
    {
        KeysetPage<SupplyRequestSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, ISupplyRequestService service)
    {
        SupplyRequestSummary? request = await service.GetAsync(id, httpContext.RequestAborted);
        return request is null ? Results.NotFound() : Results.Ok(request);
    }

    private static async Task<IResult> AddLineAsync(Guid id, AddSupplyRequestLineRequest request, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRequestRestaurantScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new AddSupplyRequestItemCommand(request.ItemId, request.UnitId, request.RequestedQuantity, request.Notes);
        var result = await service.AddLineAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> UpdateLineAsync(Guid id, Guid lineId, UpdateSupplyRequestLineRequest request, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRequestRestaurantScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new UpdateSupplyRequestItemCommand(request.UnitId, request.RequestedQuantity, request.Notes);
        var result = await service.UpdateLineAsync(id, lineId, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> RemoveLineAsync(Guid id, Guid lineId, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRequestRestaurantScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.RemoveLineAsync(id, lineId, httpContext.RequestAborted);
        return result.Succeeded ? Results.NoContent() : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> SubmitAsync(Guid id, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRequestRestaurantScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.SubmitAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> CancelAsync(Guid id, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRequestRestaurantScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.CancelAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult?> CheckRequestRestaurantScopeAsync(Guid requestId, HttpContext httpContext, ISupplyRequestService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        SupplyRequestSummary? request = await service.GetAsync(requestId, httpContext.RequestAborted);
        if (request is null)
        {
            return Results.NotFound();
        }

        return await CheckRestaurantScopeAsync(request.RestaurantId, httpContext, scopeGuard, currentUser);
    }

    private static async Task<IResult?> CheckRestaurantScopeAsync(Guid restaurantId, HttpContext httpContext, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        if (currentUser.Role != RoleName.RestaurantSupervisor)
        {
            // Owner/Admin are unrestricted within the tenant (docs/03 §Supply Requests row);
            // every other role holding supply_requests:create is a RestaurantSupervisor.
            return null;
        }

        IReadOnlySet<Guid> authorizedRestaurantIds = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
        if (authorizedRestaurantIds.Contains(restaurantId))
        {
            return null;
        }

        await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
        return Results.Empty;
    }
}
