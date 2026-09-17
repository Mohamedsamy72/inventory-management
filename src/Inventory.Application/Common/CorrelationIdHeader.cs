namespace Inventory.Application.Common;

/// <summary>The request/response header name carrying the correlation id (task 4.13, docs/21
/// §3). Lives here - not in <c>Inventory.Api.Middleware.CorrelationIdMiddleware</c>, which sets
/// it - so <c>Inventory.Infrastructure</c>'s audit logger can read the SAME constant without
/// creating a reverse dependency on Api (ADR-002: the dependency direction is inward only).</summary>
public static class CorrelationIdHeader
{
    public const string Name = "X-Correlation-Id";
}
