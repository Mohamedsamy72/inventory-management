using Inventory.Api.Errors;
using Inventory.Api.Middleware;
using Inventory.Application.Common;
using Inventory.Application.Receiving;
using Inventory.Application.Stock;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Api.Features.Receiving;

public sealed record CreateReceivingOrderRequest(Guid WarehouseId, Guid? SupplierId, DateOnly BusinessDate);
public sealed record AddReceivingOrderLineRequest(Guid ItemId, Guid UnitId, decimal ExpectedQuantity, decimal UnitCost, string? Notes);
public sealed record VerifyReceivingOrderLineRequest(Guid LineId, decimal ActualQuantity);
public sealed record VerifyReceivingOrderRequest(IReadOnlyList<VerifyReceivingOrderLineRequest> Lines);
public sealed record ReverseReceivingOrderRequest(string Reason);
public sealed record RemoveStockItemRequest(string Password, string? Reason);
public sealed record SetStockRequest(decimal Quantity, decimal? UnitCost, string? Reason);

/// <summary>Task 8.9: <see cref="IScopeGuard"/>'s first real consumer - Warehouse Staff (scoped
/// per docs/03 §Receiving row) may only act on a warehouse in its scope set; Owner/Admin are
/// unrestricted within the tenant. Out-of-tenant is already invisible via the EF query filter
/// (404 from <see cref="TransactionalErrorWriter"/>'s NotFound branch); out-of-scope-but-in-tenant
/// is the 403 this filter adds (docs/09 §2.1's IDOR-prevention distinction).</summary>
public static class ReceivingOrdersEndpoints
{
    public static IEndpointRouteBuilder MapReceivingOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder orders = app.MapGroup("/api/v1/receiving-orders");

        orders.MapPost("/", CreateAsync)
            .RequireAuthorization("receiving:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        orders.MapGet("/", ListAsync).RequireAuthorization("receiving:view");
        orders.MapGet("/{id:guid}", GetAsync).RequireAuthorization("receiving:view");
        orders.MapPost("/{id:guid}/lines", AddLineAsync)
            .RequireAuthorization("receiving:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        orders.MapDelete("/{id:guid}/lines/{lineId:guid}", RemoveLineAsync)
            .RequireAuthorization("receiving:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        orders.MapPost("/{id:guid}/submit", SubmitAsync)
            .RequireAuthorization("receiving:submit")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireIdempotencyKey();
        orders.MapPost("/{id:guid}/verify", VerifyAsync)
            .RequireAuthorization("receiving:verify")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireIdempotencyKey();
        orders.MapPost("/{id:guid}/reverse", ReverseAsync)
            .RequireAuthorization("receiving:reverse")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireIdempotencyKey();

        app.MapGet("/api/v1/warehouses/{id:guid}/stock", GetWarehouseStockAsync).RequireAuthorization("receiving:view");
        app.MapPut("/api/v1/warehouses/{id:guid}/stock/{itemId:guid}", SetStockAsync)
            .RequireAuthorization("stock:direct_set")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        app.MapPost("/api/v1/warehouses/{id:guid}/stock/{itemId:guid}/remove", RemoveStockItemAsync)
            .RequireAuthorization("stock:direct_set")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateReceivingOrderRequest request, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckWarehouseScopeAsync(request.WarehouseId, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new CreateReceivingOrderCommand(request.WarehouseId, request.SupplierId, request.BusinessDate);
        var result = await service.CreateDraftAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/receiving-orders/{result.Value!.Id}", result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IReceivingOrderService service, int? limit, string? cursor)
    {
        KeysetPage<ReceivingOrderSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IReceivingOrderService service)
    {
        ReceivingOrderSummary? order = await service.GetAsync(id, httpContext.RequestAborted);
        return order is null ? Results.NotFound() : Results.Ok(order);
    }

    private static async Task<IResult> AddLineAsync(Guid id, AddReceivingOrderLineRequest request, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckOrderWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new AddReceivingOrderLineCommand(request.ItemId, request.UnitId, request.ExpectedQuantity, request.UnitCost, request.Notes);
        var result = await service.AddLineAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> RemoveLineAsync(Guid id, Guid lineId, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckOrderWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.RemoveLineAsync(id, lineId, httpContext.RequestAborted);
        return result.Succeeded ? Results.NoContent() : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> SubmitAsync(Guid id, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckOrderWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        IdempotencyContext idempotency = await BuildIdempotencyContextAsync(httpContext);
        var result = await service.SubmitAsync(id, idempotency, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> VerifyAsync(Guid id, VerifyReceivingOrderRequest request, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckOrderWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        IdempotencyContext idempotency = await BuildIdempotencyContextAsync(httpContext);
        var command = new VerifyReceivingOrderCommand(request.Lines.Select(l => new VerifyReceivingOrderLineCommand(l.LineId, l.ActualQuantity)).ToList());
        var result = await service.VerifyAsync(id, command, idempotency, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> ReverseAsync(Guid id, ReverseReceivingOrderRequest request, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckOrderWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        IdempotencyContext idempotency = await BuildIdempotencyContextAsync(httpContext);
        var result = await service.ReverseAsync(id, request.Reason, idempotency, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    /// <summary>Every route calling this carries <c>.RequireIdempotencyKey()</c> with the default
    /// <see cref="IdempotencyRequirement.Required"/>, so <see cref="IdempotencyMiddleware"/> has
    /// already rejected the request with 400 if the header were missing - it is always present
    /// here. The raw body text is re-read from the SAME buffered request stream the middleware
    /// hashed (<c>EnableBuffering</c> makes it seekable), so the hash this records matches
    /// exactly what a future identical retry will compute - reading a model-bound/re-serialized
    /// object here instead would silently break replay detection.</summary>
    private static async Task<IdempotencyContext> BuildIdempotencyContextAsync(HttpContext httpContext)
    {
        string key = httpContext.Request.Headers[IdempotencyMiddleware.HeaderName].ToString();
        string endpoint = $"{httpContext.Request.Method} {httpContext.Request.Path}";

        httpContext.Request.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync(httpContext.RequestAborted);

        return new IdempotencyContext(key, endpoint, rawBody);
    }

    private static async Task<IResult> GetWarehouseStockAsync(Guid id, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckWarehouseScopeAsync(id, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        return Results.Ok(await service.GetWarehouseStockAsync(id, httpContext.RequestAborted));
    }

    private static async Task<IResult> SetStockAsync(
        Guid id, Guid itemId, SetStockRequest request, HttpContext httpContext, IStockAdjustmentService service, IScopeGuard scopeGuard, ICurrentUserService currentUser, IFinancialProjection financialProjection)
    {
        if (request.Quantity < 0 || request.UnitCost < 0)
        {
            return Results.BadRequest();
        }

        // Entering a cost is a financial input: only an account that may see costs may supply one.
        if (request.UnitCost is not null && !financialProjection.IsVisible)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
            return Results.Empty;
        }

        IResult? forbidden = await CheckWarehouseScopeAsync(id, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.SetStockAsync(id, itemId, request.Quantity, request.UnitCost, request.Reason, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    /// <summary>Removing an item from the stock is destructive, so the caller's PASSWORD is re-verified first (with the same
    /// lockout as login, so it cannot be brute-forced from a hijacked session). The password is never logged, audited or echoed.</summary>
    private static async Task<IResult> RemoveStockItemAsync(
        Guid id, Guid itemId, RemoveStockItemRequest request, HttpContext httpContext, IStockAdjustmentService service, IScopeGuard scopeGuard,
        ICurrentUserService currentUser, UserManager<User> userManager, SignInManager<User> signInManager)
    {
        IResult? forbidden = await CheckWarehouseScopeAsync(id, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        User? user = await userManager.FindByIdAsync(currentUser.UserId.ToString());
        if (user is null || string.IsNullOrEmpty(request.Password))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.PasswordConfirmationFailed);
            return Results.Empty;
        }

        Microsoft.AspNetCore.Identity.SignInResult check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimitExceeded);
            return Results.Empty;
        }

        if (!check.Succeeded)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.PasswordConfirmationFailed);
            return Results.Empty;
        }

        var result = await service.RemoveItemAsync(id, itemId, request.Reason, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult?> CheckOrderWarehouseScopeAsync(Guid orderId, HttpContext httpContext, IReceivingOrderService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        ReceivingOrderSummary? order = await service.GetAsync(orderId, httpContext.RequestAborted);
        if (order is null)
        {
            return Results.NotFound();
        }

        return await CheckWarehouseScopeAsync(order.WarehouseId, httpContext, scopeGuard, currentUser);
    }

    private static async Task<IResult?> CheckWarehouseScopeAsync(Guid warehouseId, HttpContext httpContext, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        if (currentUser.Role != RoleName.WarehouseStaff)
        {
            // Owner/Admin are unrestricted within the tenant (docs/03 §Receiving row); every
            // other role holding a receiving permission at all is WarehouseStaff.
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
