using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Inventory.Api.RateLimiting;

/// <summary>
/// Task 3.11 (docs/08 §5, ADR-026). Named policies for every rate-limit row that can be keyed
/// on IP address or authenticated user - both available before model binding runs, so the
/// framework's <c>UseRateLimiter()</c> middleware can partition on them directly. Lives in the
/// Api project (not Infrastructure) because <c>AddRateLimiter</c> is an ASP.NET Core hosting
/// concern that only resolves in a Web SDK compile context - matching where
/// <c>AddProblemDetails</c>/<c>AddExceptionHandler</c> already live in <c>Program.cs</c>.
/// <para>
/// The OTP endpoints' per-MOBILE windows (3/15min, 5/hour) key on data that only exists in the
/// parsed request body, which this middleware runs before model binding resolves - those two
/// windows are enforced explicitly inside the endpoint handler via
/// <c>Inventory.Application.Auth.IOtpAttemptLimiter</c> instead, not through a named policy here.
/// </para>
/// </summary>
public static class RateLimitingExtensions
{
    public const string LoginPolicy = "auth-login";
    public const string OtpIpPolicy = "auth-otp-ip";
    public const string SensitivePolicy = "sensitive-mutation";

    public static IServiceCollection AddInventoryRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // General: 300 requests per user (or per IP if unauthenticated) per minute.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(httpContext),
                    factory: static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // Login: 5 requests per IP per minute (docs/08 §5).
            options.AddPolicy(LoginPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: IpAddress(httpContext),
                    factory: static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // OTP, per-IP component only (docs/08 §5's "3 per IP / Mobile" row) - the per-mobile
            // windows are enforced separately by IOtpAttemptLimiter, see the type doc above.
            options.AddPolicy(OtpIpPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: IpAddress(httpContext),
                    factory: static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0,
                    }));

            // Sensitive mutations (/confirm, /submit, etc. - none exist before Phase 8):
            // 30 requests per authenticated user per minute.
            options.AddPolicy(SensitivePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: PartitionKey(httpContext),
                    factory: static _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string PartitionKey(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? $"user:{httpContext.User.FindFirst("user_id")?.Value}"
            : $"ip:{IpAddress(httpContext)}";

    private static string IpAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
