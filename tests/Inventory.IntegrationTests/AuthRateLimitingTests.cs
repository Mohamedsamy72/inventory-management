using System.Net;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>
/// Task 3.11/3.14 - rate-limit and lockout behaviour (docs/08 §5, ADR-026). Deliberately
/// separate from <see cref="AuthEndpointTests"/>: <see cref="WebApplicationFactory{TEntryPoint}"/>'s
/// rate-limiter and OTP-attempt-limiter singletons are shared by every test within one
/// IClassFixture instance, and a test here that intentionally exhausts a budget would otherwise
/// starve unrelated tests' login/OTP calls for the rest of that run - this class gets its own
/// fixture instance, hence its own isolated singletons.
/// </summary>
public sealed class AuthRateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthRateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Repeated_Failures_Lock_The_Account_Out()
    {
        (User user, _) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        // IdentityOptions.Lockout.MaxFailedAccessAttempts = 5, and the login rate limit is also
        // 5/IP/min (docs/08 §5) - exactly 5 attempts stays within the rate-limit budget while
        // still crossing the lockout threshold on the 5th; a 6th call would be rejected by the
        // rate limiter instead of exercising the lockout path this test is actually about.
        HttpResponseMessage? lastResponse = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            lastResponse?.Dispose();
            lastResponse = await AuthTestHelpers.PostJsonAsync(
                client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password = "wrong-password" });
        }

        Assert.Equal(HttpStatusCode.Unauthorized, lastResponse!.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        User reloaded = await context.Users.IgnoreQueryFilters().AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.NotNull(reloaded.LockoutEndAt);
        Assert.True(reloaded.LockoutEndAt > DateTimeOffset.UtcNow);
        lastResponse.Dispose();
    }

    [Fact]
    public async Task Otp_Ip_Rate_Limit_Trips_After_Three_Requests_In_The_Window()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        HttpResponseMessage? last = null;
        for (int i = 0; i < 4; i++)
        {
            last?.Dispose();
            // Distinct mobile each time, isolating the IP window from the per-mobile one.
            string mobile = "01097" + Guid.NewGuid().ToString("N")[..5];
            last = await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/forgot-password/otp", new { mobileNumber = mobile });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        last.Dispose();
    }

    [Fact]
    public async Task Otp_Mobile_Rate_Limit_Trips_After_Three_Requests_In_Fifteen_Minutes()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);
        string mobile = "01096" + Guid.NewGuid().ToString("N")[..5];

        // The SAME client/mobile pair every time - the per-IP limiter (3/15min) and the
        // per-mobile limiter (3/15min) are both exactly 3, so the 4th request trips whichever
        // is checked; either way it must be a 429.
        HttpResponseMessage? last = null;
        for (int i = 0; i < 4; i++)
        {
            last?.Dispose();
            last = await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/forgot-password/otp", new { mobileNumber = mobile });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        last.Dispose();
    }

    [Fact]
    public async Task Login_Is_Rate_Limited_To_Five_Per_Ip_Per_Minute()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        HttpResponseMessage? last = null;
        for (int i = 0; i < 6; i++)
        {
            last?.Dispose();
            last = await AuthTestHelpers.PostJsonAsync(
                client,
                "/api/v1/auth/login",
                new { mobileNumber = "01095" + Guid.NewGuid().ToString("N")[..5], password = "whatever" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        last.Dispose();
    }
}
