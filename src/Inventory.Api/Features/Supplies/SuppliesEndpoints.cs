using Inventory.Api.Errors;
using Inventory.Api.Middleware;
using Inventory.Application.Common;
using Inventory.Application.Supplies;
using Inventory.Application.SupplyRequests;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.Supplies;

public sealed record FulfillSupplyLineRequest(Guid SupplyRequestItemId, decimal FulfilledQuantity);
public sealed record FulfillSupplyRequestRequest(IReadOnlyList<FulfillSupplyLineRequest> Lines);
public sealed record ConfirmSupplyLineRequest(Guid SupplyItemId, decimal ReceivedQuantity);
public sealed record ConfirmSupplyRequestRequest(IReadOnlyList<ConfirmSupplyLineRequest> Lines);
public sealed record DirectIssueLineRequest(Guid ItemId, Guid UnitId, decimal Quantity);
public sealed record DirectIssueRequest(Guid WarehouseId, Guid RestaurantId, IReadOnlyList<DirectIssueLineRequest> Lines);

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
        supplies.MapPost("/{id:guid}/confirm", ConfirmAsync)
            .RequireAuthorization("supplies:confirm")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireIdempotencyKey();

        // Change 4: Owner/Admin-only direct issue (supplies:direct_issue is seeded to no other role).
        supplies.MapPost("/direct-issue", DirectIssueAsync)
            .RequireAuthorization("supplies:direct_issue")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireIdempotencyKey();

        return app;
    }

    private static async Task<IResult> DirectIssueAsync(DirectIssueRequest request, HttpContext httpContext, ISupplyService service)
    {
        IdempotencyContext idempotency = await BuildIdempotencyContextAsync(httpContext);
        var command = new DirectIssueCommand(request.WarehouseId, request.RestaurantId, request.Lines.Select(l => new DirectIssueLineCommand(l.ItemId, l.UnitId, l.Quantity)).ToList());
        var result = await service.DirectIssueAsync(command, idempotency, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/supplies/{result.Value!.Supply.Id}", result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
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

    /// <summary>Task 11.8: restaurant scope, not warehouse scope - the confirming actor is the
    /// restaurant side of this document. Owner/Admin (and Warehouse Staff, which does not hold
    /// `supplies:confirm` at all per docs/03) are unaffected.</summary>
    private static async Task<IResult> ConfirmAsync(Guid id, ConfirmSupplyRequestRequest request, HttpContext httpContext, ISupplyService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        SupplySummary? supply = await service.GetAsync(id, httpContext.RequestAborted);
        if (supply is null)
        {
            return Results.NotFound();
        }

        if (currentUser.Role == RoleName.RestaurantSupervisor)
        {
            IReadOnlySet<Guid> authorizedRestaurantIds = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
            if (!authorizedRestaurantIds.Contains(supply.RestaurantId))
            {
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
                return Results.Empty;
            }
        }

        IdempotencyContext idempotency = await BuildIdempotencyContextAsync(httpContext);
        var command = new ConfirmSupplyCommand(request.Lines.Select(l => new ConfirmSupplyLineCommand(l.SupplyItemId, l.ReceivedQuantity)).ToList());
        var result = await service.ConfirmAsync(id, command, idempotency, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    /// <summary>Mirrors <c>ReceivingOrdersEndpoints</c>'s identical helper - every route calling
    /// this carries <c>.RequireIdempotencyKey()</c> (default <c>Required</c>), so the header is
    /// always present by the time the handler runs. Re-reading the raw buffered body (rather
    /// than re-serializing the bound request object) keeps the hash identical to what
    /// <c>IdempotencyMiddleware</c> already computed for THIS request.</summary>
    private static async Task<IdempotencyContext> BuildIdempotencyContextAsync(HttpContext httpContext)
    {
        string key = httpContext.Request.Headers[IdempotencyMiddleware.HeaderName].ToString();
        string endpoint = $"{httpContext.Request.Method} {httpContext.Request.Path}";

        httpContext.Request.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync(httpContext.RequestAborted);

        return new IdempotencyContext(key, endpoint, rawBody);
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
