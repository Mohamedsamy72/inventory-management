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

    private static async Task<IResult> ListAsync(HttpContext httpContext, IConsumptionService service, IScopeGuard scopeGuard, ICurrentUserService currentUser, Guid? restaurantId, int? limit, string? cursor)
    {
        if (currentUser.Role == RoleName.RestaurantSupervisor)
        {
            IReadOnlySet<Guid> authorizedRestaurantIds = await scopeGuard.GetAuthorizedRestaurantIdsAsync(currentUser.UserId, httpContext.RequestAborted);
            if (restaurantId is { } requested)
            {
                if (!authorizedRestaurantIds.Contains(requested))
                {
                    await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
                    return Results.Empty;
                }
            }
            else if (authorizedRestaurantIds.Count != 1)
            {
                // No single implicit restaurant to default to - require an explicit, in-scope id.
                await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status403Forbidden, ErrorCodes.ForbiddenScope);
                return Results.Empty;
            }
            else
            {
                restaurantId = authorizedRestaurantIds.Single();
            }
        }

        KeysetPage<ConsumptionRecordSummary> page = await service.ListAsync(restaurantId, KeysetPagination.ClampLimit(limit), cursor, httpContext.RequestAborted);
        return Results.Ok(page);
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
