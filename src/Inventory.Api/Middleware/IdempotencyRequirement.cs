using Microsoft.AspNetCore.Builder;

namespace Inventory.Api.Middleware;

/// <summary>Task 7.9's docs/30 §6.1 matrix - whether an endpoint demands
/// <c>X-Idempotency-Key</c> or merely honours it when supplied.</summary>
public enum IdempotencyRequirement
{
    Required,
    Optional,
}

/// <summary>Endpoint metadata <see cref="IdempotencyMiddleware"/> reads to decide whether/how to
/// intercept a request. Its mere presence (regardless of <see cref="Requirement"/>) is what opts
/// an endpoint into idempotency handling at all - an endpoint with no such metadata is untouched
/// by the middleware, matching docs/30 §6.1's "all other mutations: optional" row without every
/// single endpoint needing to declare that explicitly.</summary>
public sealed class IdempotencyMetadata
{
    public IdempotencyMetadata(IdempotencyRequirement requirement)
    {
        Requirement = requirement;
    }

    public IdempotencyRequirement Requirement { get; }
}

/// <summary>Mirrors the <c>.RequireAuthorization(...)</c> convention-builder pattern already used
/// throughout this codebase for opting an endpoint into a cross-cutting concern.</summary>
public static class IdempotencyEndpointConventionBuilderExtensions
{
    public static TBuilder RequireIdempotencyKey<TBuilder>(
        this TBuilder builder, IdempotencyRequirement requirement = IdempotencyRequirement.Required)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new IdempotencyMetadata(requirement));
        return builder;
    }
}
