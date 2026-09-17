using Inventory.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Errors;

/// <summary>
/// Writes an RFC 7807 <see cref="ProblemDetails"/> response for a known docs/13 §2 business
/// error code, in the exact same shape <see cref="GlobalExceptionHandler"/> uses for unexpected
/// failures: <c>code</c>, <c>messageAr</c>, <c>correlationId</c> always; <c>messageEn</c> only
/// in Development (docs/31 §4.1, CR-072). One writer, so every endpoint's error shape stays
/// identical rather than drifting per handler.
/// </summary>
internal static class ProblemResponseWriter
{
    public static async Task WriteAsync(HttpContext httpContext, int statusCode, string code)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string correlationId =
            httpContext.Items[CorrelationIdMiddleware.HeaderName] as string
            ?? httpContext.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Type = $"https://errors.inventory.local/{code}",
            Title = code,
            Status = statusCode,
            Instance = httpContext.Request.Path.Value,
        };

        problem.Extensions["code"] = code;
        problem.Extensions["messageAr"] = ErrorCodes.ArabicMessageFor(code);
        problem.Extensions["correlationId"] = correlationId;

        IHostEnvironment environment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment())
        {
            problem.Extensions["messageEn"] = ErrorCodes.EnglishMessageFor(code);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem);
    }
}
