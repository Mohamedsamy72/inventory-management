using Inventory.Application.Audit;
using Inventory.Application.Common;

namespace Inventory.Api.Features.Audit;

/// <summary>Task 14.1-14.4. `audit:view`/`audit:export` are both `NON_GRANTABLE` (ADR-012/013):
/// <c>PermissionAuthorizationHandler</c> denies every role except Owner outright, even in the
/// presence of a stray grant row - nothing in this file re-implements that check, and nothing
/// here needs to, which is the point (task 4.8's guard is the one place it can't be forgotten).</summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/audit", ListAsync).RequireAuthorization("audit:view");
        app.MapGet("/api/v1/audit/activity", ListActivityAsync).RequireAuthorization("audit:view");
        app.MapGet("/api/v1/audit/export", ExportAsync).RequireAuthorization("audit:export");

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext, IAuditReaderService service, Guid? actorUserId, string? entityType, DateTimeOffset? from, DateTimeOffset? to, int? limit, string? cursor)
    {
        var filter = new AuditLogFilter(actorUserId, entityType, from, to);
        KeysetPage<AuditLogSummary> page = await service.ListAsync(filter, KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> ListActivityAsync(
        HttpContext httpContext, IAuditReaderService service, Guid? actorUserId, string? entityType, DateTimeOffset? from, DateTimeOffset? to, int? limit, string? cursor)
    {
        var filter = new AuditLogFilter(actorUserId, entityType, from, to);
        KeysetPage<AuditActivitySummary> page = await service.ListActivityAsync(filter, KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> ExportAsync(
        HttpContext httpContext, IAuditReaderService service, Guid? actorUserId, string? entityType, DateTimeOffset? from, DateTimeOffset? to, int? limit, string? cursor)
    {
        var filter = new AuditLogFilter(actorUserId, entityType, from, to);
        KeysetPage<AuditLogSummary> page = await service.ExportAsync(filter, KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }
}
