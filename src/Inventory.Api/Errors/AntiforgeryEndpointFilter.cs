using Inventory.Api.Middleware;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Errors;

/// <summary>
/// Task 3.7 (docs/08 §4): validates the X-CSRF-TOKEN header on every mutating request this
/// filter is applied to.
/// </summary>
/// <remarks>
/// Minimal API endpoints do NOT get automatic antiforgery validation from
/// <c>app.UseAntiforgery()</c> the way Razor Pages/MVC views do - that middleware only acts on
/// endpoints carrying <c>IAntiforgeryMetadata</c>, which nothing attaches to a plain
/// <c>MapPost</c> lambda. Verified by direct reproduction: an unprotected minimal API endpoint
/// accepted a mutating POST with zero CSRF token and no rejection. This filter calls
/// <see cref="IAntiforgery.ValidateRequestAsync"/> explicitly instead of relying on the
/// middleware to have already done it.
/// </remarks>
internal sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            string correlationId =
                context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] as string
                ?? context.HttpContext.TraceIdentifier;

            var problem = new ProblemDetails
            {
                Type = "https://errors.inventory.local/CSRF_TOKEN_INVALID",
                Title = "CSRF_TOKEN_INVALID",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path.Value,
            };
            problem.Extensions["code"] = "CSRF_TOKEN_INVALID";
            problem.Extensions["messageAr"] = "الطلب غير موثوق، يرجى إعادة تحميل الصفحة والمحاولة مرة أخرى.";
            problem.Extensions["correlationId"] = correlationId;

            return Results.Problem(
                detail: null,
                statusCode: StatusCodes.Status400BadRequest,
                type: problem.Type,
                title: problem.Title,
                extensions: problem.Extensions);
        }

        return await next(context);
    }
}
