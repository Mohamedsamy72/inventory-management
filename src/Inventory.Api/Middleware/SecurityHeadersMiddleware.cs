namespace Inventory.Api.Middleware;

/// <summary>
/// Task S1 (docs/09, docs/20 §2). docs/20 places CSP/HSTS/X-Frame-Options at the reverse proxy
/// in production, but this API has no reverse-proxy layer of its own in this repository - so the
/// headers are also set here, defense in depth, rather than depending entirely on infrastructure
/// this codebase does not configure. Applied to every response, including error responses
/// (added before the pipeline branches), since an attacker-facing error page is exactly the kind
/// of response these headers protect.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(static state =>
        {
            var httpContext = (HttpContext)state;
            IHeaderDictionary headers = httpContext.Response.Headers;

            headers["X-Frame-Options"] = "DENY";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // A pure JSON API serves no HTML, scripts, styles, or images of its own - the
            // strictest possible policy is also the correct one, not an arbitrary hardening
            // choice.
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

            return Task.CompletedTask;
        }, context);

        return _next(context);
    }
}
