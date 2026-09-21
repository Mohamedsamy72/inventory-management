using Inventory.Api.Errors;
using Inventory.Application.Common;
using Inventory.Application.Consumption;
using Inventory.Domain.Enums;

namespace Inventory.Api.Features.Consumption;

public sealed record RecordConsumptionRequest(Guid RestaurantId, Guid ItemId, decimal Quantity, Guid UnitId, DateOnly ConsumptionDate, string? Notes);

/// <summary>REQ-07 (docs/26, docs/04 §12). <see cref="IScopeGuard"/>'s consumer for this
/// resource: Restaurant Supervisor may only log/view consumption for their own restaurants;
/// Owner/Admin are unrestricted.</summary>
public static class ConsumptionEndpoints
{
    public static IEndpointRouteBuilder MapConsumptionEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder consumption = app.MapGroup("/api/v1/consumption");

        consumption.MapPost("/", RecordAsync)
            .RequireAuthorization("consumption:create")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        consumption.MapGet("/", ListAsync).RequireAuthorization("consumption:view");
        // Print/report data for exactly the same filter + scope as the list (docs/27 section 37).
        consumption.MapGet("/report", ReportAsync).RequireAuthorization("consumption:view");

        return app;
    }

    private static async Task<IResult> RecordAsync(RecordConsumptionRequest request, HttpContext httpContext, IConsumptionService service, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        IResult? forbidden = await CheckRestaurantScopeAsync(request.RestaurantId, httpContext, scopeGuard, currentUser);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var command = new RecordConsumptionCommand(request.RestaurantId, request.ItemId, request.Quantity, request.UnitId, request.ConsumptionDate, request.Notes);
        var result = await service.RecordAsync(command, httpContext.RequestAborted);
        return result.Succeeded ? Results.Created($"/api/v1/consumption/{result.Value!.Id}", result.Value) : await MasterDataErrorWriter.WriteErrorAsync(httpContext, result.Error);
    }

    private const int MaxRangeDays = 366;

    private static async Task<IResult> ListAsync(HttpContext httpContext, IConsumptionService service, IScopeGuard scopeGuard, ICurrentUserService currentUser, Guid? restaurantId, DateOnly? from, DateOnly? to, int? limit, string? cursor)
    {
        (Guid? effectiveRestaurantId, IResult? rejected) = await ResolveFilterAsync(httpContext, scopeGuard, currentUser, restaurantId, from, to);
        if (rejected is not null)
        {
            return rejected;
        }

        KeysetPage<ConsumptionRecordSummary> page = await service.ListAsync(effectiveRestaurantId, from, to, KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
    }

    private static async Task<IResult> ReportAsync(HttpContext httpContext, IConsumptionService service, IScopeGuard scopeGuard, ICurrentUserService currentUser, Guid? restaurantId, DateOnly? from, DateOnly? to)
    {
        (Guid? effectiveRestaurantId, IResult? rejected) = await ResolveFilterAsync(httpContext, scopeGuard, currentUser, restaurantId, from, to);
        if (rejected is not null)
        {
            return rejected;
        }

        return Results.Ok(await service.GetReportAsync(effectiveRestaurantId, from, to, httpContext.RequestAborted));
    }

    /// <summary>The one place the date-range validation and the Restaurant Supervisor scope rule live, so the list and the
    /// print report can never disagree about what a caller may see. A client-supplied restaurant id never widens scope.</summary>
    private static async Task<(Guid? RestaurantId, IResult? Rejected)> ResolveFilterAsync(
        HttpContext httpContext, IScopeGuard scopeGuard, ICurrentUserService currentUser, Guid? restaurantId, DateOnly? from, DateOnly? to)
    {
        // Both dates or neither (neither = the rolling last 24 hours); from must not follow to; bounded span.
        if ((from is null) != (to is null) || (from is { } f && to is { } t && (t < f || t.DayNumber - f.DayNumber >= MaxRangeDays)))
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.InvalidDateRange);
            return (null, Results.Empty);
        }

        if (currentUser.Role == RoleName.RestaurantSupervisor)
        {
            IReadOnlySet<Guid> authorizedRestaurantIds = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
            if (restaurantId is { } requested)
            {
                if (!authorizedRestaurantIds.Contains(requested))
                {
                    await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
                    return (null, Results.Empty);
                }
            }
            else if (authorizedRestaurantIds.Count != 1)
            {
                // No single implicit restaurant to default to - require an explicit, in-scope id.
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
                return (null, Results.Empty);
            }
            else
            {
                restaurantId = authorizedRestaurantIds.Single();
            }
        }

        return (restaurantId, null);
    }

    private static async Task<IResult?> CheckRestaurantScopeAsync(Guid restaurantId, HttpContext httpContext, IScopeGuard scopeGuard, ICurrentUserService currentUser)
    {
        if (currentUser.Role != RoleName.RestaurantSupervisor)
        {
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
