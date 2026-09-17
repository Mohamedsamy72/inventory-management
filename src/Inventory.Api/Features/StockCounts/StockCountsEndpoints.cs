using Inventory.Api.Errors;
using Inventory.Api.Middleware;
using Inventory.Application.Common;
using Inventory.Application.StockCounts;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.StockCounts;

public sealed record CreateStockCountRequest(Guid WarehouseId, bool IsBlindCount);
public sealed record RecordStockCountLineRequest(Guid LineId, decimal PhysicalQuantity);
public sealed record RecordStockCountRequest(IReadOnlyList<RecordStockCountLineRequest> Lines);

/// <summary>Task 13.6/docs/03: <c>stock_counts:approve</c> is granted only to Owner/Admin - no
/// scope check is needed on approve/reject beyond that permission grant, since neither role is
/// ever warehouse-scoped. Task 13.11's "warehouse staff cannot approve" is therefore enforced
/// entirely by <c>.RequireAuthorization("stock_counts:approve")</c>, not by any code here.
/// <see cref="IScopeGuard"/>'s sixth consumer - Warehouse Staff on create/record/submit.</summary>
public static class StockCountsEndpoints
{
    public static IEndpointRouteBuilder MapStockCountsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder counts = app.MapGroup("/api/v1/stock-counts");

        counts.MapPost("/", CreateAsync)
            .RequireAuthorization("stock_counts:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        counts.MapGet("/", ListAsync).RequireAuthorization("stock_counts:view");
        counts.MapGet("/{id:guid}", GetAsync).RequireAuthorization("stock_counts:view");
        counts.MapPost("/{id:guid}/record", RecordAsync)
            .RequireAuthorization("stock_counts:count")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        counts.MapPost("/{id:guid}/submit", SubmitForApprovalAsync)
            .RequireAuthorization("stock_counts:count")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        counts.MapPost("/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization("stock_counts:approve")
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireIdempotencyKey();
        counts.MapPost("/{id:guid}/reject", RejectAsync)
            .RequireAuthorization("stock_counts:approve")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(CreateStockCountRequest request, HttpContext httpContext, IStockCountService service)
    {
        // No scope check needed: stock_counts:create is Owner/Admin only (docs/03), neither of
        // which is ever warehouse-scoped - Warehouse Staff cannot reach this handler at all.
        var command = new CreateStockCountCommand(request.WarehouseId, request.IsBlindCount);
        var result = await service.CreateAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/stock-counts/{result.Value!.Id}", result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> ListAsync(HttpContext httpContext, IStockCountService service, int? limit, string? cursor)
    {
        KeysetPage<StockCountSummary> page = await service.ListAsync(KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext httpContext, IStockCountService service)
    {
        StockCountSummary? stockCount = await service.GetAsync(id, httpContext.RequestAborted);
        return stockCount is null ? Results.NotFound() : Results.Ok(stockCount);
    }

    private static async Task<IResult> RecordAsync(Guid id, RecordStockCountRequest request, HttpContext httpContext, IStockCountService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckCountWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new RecordStockCountCommand(request.Lines.Select(l => new RecordStockCountLineCommand(l.LineId, l.PhysicalQuantity)).ToList());
        var result = await service.RecordAsync(id, command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> SubmitForApprovalAsync(Guid id, HttpContext httpContext, IStockCountService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckCountWarehouseScopeAsync(id, httpContext, service, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var result = await service.SubmitForApprovalAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> ApproveAsync(Guid id, HttpContext httpContext, IStockCountService service)
    {
        IdempotencyContext idempotency = await BuildIdempotencyContextAsync(httpContext);
        var result = await service.ApproveAsync(id, idempotency, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IResult> RejectAsync(Guid id, HttpContext httpContext, IStockCountService service)
    {
        var result = await service.RejectAsync(id, httpContext.RequestAborted);
        return result.Succeeded ? Results.Ok(result.Value) : await TransactionalErrorWriter.WriteErrorAsync(httpContext, result.Error, result.ErrorDetail);
    }

    private static async Task<IdempotencyContext> BuildIdempotencyContextAsync(HttpContext httpContext)
    {
        string key = httpContext.Request.Headers[IdempotencyMiddleware.HeaderName].ToString();
        string endpoint = $"{httpContext.Request.Method} {httpContext.Request.Path}";

        httpContext.Request.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        string rawBody = await reader.ReadToEndAsync(httpContext.RequestAborted);

        return new IdempotencyContext(key, endpoint, rawBody);
    }

    private static async Task<IResult?> CheckCountWarehouseScopeAsync(Guid countId, HttpContext httpContext, IStockCountService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        StockCountSummary? stockCount = await service.GetAsync(countId, httpContext.RequestAborted);
        if (stockCount is null)
        {
            return Results.NotFound();
        }

        return await CheckWarehouseScopeAsync(stockCount.WarehouseId, httpContext, scopeGuard, currentUser);
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
