using Inventory.Api.Errors;
using Inventory.Application.Common;

namespace Inventory.Api.Middleware;

/// <summary>
/// Task 7.9 (docs/30 §6.1). Acts only on endpoints carrying <see cref="IdempotencyMetadata"/>
/// (via <c>.RequireIdempotencyKey()</c>) - everything else passes through untouched, matching
/// docs/30 §6.1's "all other mutations: optional, accepted where supplied" row without every
/// endpoint needing to opt in explicitly.
/// </summary>
/// <remarks>
/// This middleware only CHECKS and REJECTS (missing-required-key, key-reuse, replay). It does
/// NOT call <see cref="IIdempotencyService.RecordResponse"/> - only the handler that produced the
/// response knows the role-projected DTO to cache (task 7.10: the domain object must never be
/// cached), so recording is the handler's own responsibility, as part of its own business
/// transaction, not something generic middleware sitting outside that transaction could do
/// correctly.
/// </remarks>
public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "X-Idempotency-Key";

    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IdempotencyMetadata? metadata = context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyMetadata>();
        if (metadata is null)
        {
            await _next(context);
            return;
        }

        string? idempotencyKey = context.Request.Headers.TryGetValue(HeaderName, out var values) ? values.ToString() : null;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            if (metadata.Requirement == IdempotencyRequirement.Required)
            {
                await ProblemResponseWriter.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.IdempotencyKeyRequired);
                return;
            }

            await _next(context);
            return;
        }

        // Buffer the body so both this middleware and the eventual handler can each read it in
        // full - the request stream is forward-only otherwise.
        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted);
        }

        context.Request.Body.Position = 0;

        var idempotencyService = context.RequestServices.GetRequiredService<IIdempotencyService>();
        string endpoint = $"{context.Request.Method} {context.Request.Path}";
        IdempotencyCheckResult checkResult = await idempotencyService.CheckAsync(idempotencyKey, endpoint, body, context.RequestAborted);

        switch (checkResult.Outcome)
        {
            case IdempotencyOutcome.Replay:
                // docs/30 §6.2 step 4: return the cached response verbatim, no business work.
                context.Response.StatusCode = checkResult.CachedStatusCode;
                if (checkResult.CachedPayloadJson is not null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(checkResult.CachedPayloadJson, context.RequestAborted);
                }

                return;

            case IdempotencyOutcome.KeyReuse:
                await ProblemResponseWriter.WriteAsync(context, StatusCodes.Status409Conflict, ErrorCodes.IdempotencyKeyReuse);
                return;

            case IdempotencyOutcome.New:
            default:
                await _next(context);
                return;
        }
    }
}
