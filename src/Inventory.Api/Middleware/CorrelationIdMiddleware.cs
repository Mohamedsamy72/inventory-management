using Serilog.Context;

namespace Inventory.Api.Middleware;

/// <summary>
/// Assigns every request a correlation id and pushes it into the Serilog context so
/// that each log line, and later each audit row, can be traced back to one request.
/// </summary>
/// <remarks>
/// The id is accepted from the inbound <c>X-Correlation-Id</c> header when present — so a
/// call chain keeps one id — and generated otherwise. An inbound value is length-capped and
/// character-filtered before use: it is attacker-controlled text that ends up in log files,
/// and unfiltered control characters are how log-forging works.
/// <para>
/// <c>CompanyId</c> and <c>UserId</c> enrichment (docs/21 section 3) is added in Phase 3,
/// when authentication first puts a principal on the request. Pushing empty placeholders
/// now would put misleading fields in every log line.
/// </para>
/// </remarks>
public sealed class CorrelationIdMiddleware
{
    /// <summary>The request and response header carrying the correlation id.</summary>
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxAcceptedLength = 64;

    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>Runs the middleware.</summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            string? candidate = values.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(candidate) && IsSafe(candidate))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("D");
    }

    /// <summary>
    /// Accepts only short, printable, unambiguous ids. Rejecting rather than sanitising
    /// keeps the stored value identical to what the caller sent, which is what makes it
    /// useful for tracing in the first place.
    /// </summary>
    private static bool IsSafe(string value)
    {
        if (value.Length > MaxAcceptedLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool allowed =
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_' or ':' or '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
