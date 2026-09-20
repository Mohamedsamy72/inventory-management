using Inventory.Api.Errors;
using Inventory.Application.Settings;

namespace Inventory.Api.Features.Settings;

/// <summary>The request DTO declares only the editable field - no company id, role, or scope can
/// be posted; the tenant is always the caller's own.</summary>
public sealed record UpdateSettingsRequest(string TimezoneId);

/// <summary>Change 5. `settings:manage` is seeded to Owner and Admin only. Nothing financial is
/// exposed (timezone / currency code / company name).</summary>
public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/settings", GetAsync).RequireAuthorization("settings:manage");
        app.MapPut("/api/v1/settings", UpdateAsync).RequireAuthorization("settings:manage").AddEndpointFilter<AntiforgeryEndpointFilter>();
        return app;
    }

    private static async Task<IResult> GetAsync(HttpContext httpContext, ISettingsService service) =>
        Results.Ok(await service.GetAsync(httpContext.RequestAborted));

    private static async Task<IResult> UpdateAsync(UpdateSettingsRequest request, HttpContext httpContext, ISettingsService service)
    {
        CompanySettingsSummary? updated = await service.UpdateTimezoneAsync(request.TimezoneId, httpContext.RequestAborted);
        if (updated is null)
        {
            await ProblemResponseWriter.WriteAsync(httpContext, StatusCodes.Status400BadRequest, ErrorCodes.InvalidTimezone);
            return Results.Empty;
        }

        return Results.Ok(updated);
    }
}
