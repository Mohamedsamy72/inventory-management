using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Inventory.Application.Auth;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Shared setup for the auth integration test classes. Split into
/// <see cref="AuthEndpointTests"/> (normal-flow behaviour) and <see cref="AuthRateLimitingTests"/>
/// (deliberately exhausts rate-limit budgets) specifically because
/// <see cref="WebApplicationFactory{TEntryPoint}"/> singletons - including the rate limiter and
/// <see cref="IOtpAttemptLimiter"/> - are shared across every test in one class via
/// IClassFixture, and a test that deliberately trips a shared limit would otherwise starve
/// every other test's login/OTP calls for the rest of the run. Separate classes get separate
/// fixture instances, hence separate process-wide singletons.</summary>
internal static class AuthTestHelpers
{
    public static async Task<(User User, string Password)> CreateActiveUserAsync(
        WebApplicationFactory<Program> factory, bool active = true)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        var company = new Company("Auth Test Co", "ATC" + suffix);
        context.Companies.Add(company);
        await context.SaveChangesAsync();

        // Digits only: MobileNumberNormalizer strips everything else, so a hex GUID substring
        // (which is 6/16 non-digit chars) collapses to a much shorter, collision-prone number
        // once normalized - a random 9-digit numeric string keeps the full entropy.
        string mobileSuffix = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

        const string password = "Str0ng!Passw0rd";
        var user = new User(company.Id, "Auth Test User", "01" + mobileSuffix, "placeholder", "placeholder");
        IdentityResult createResult = await userManager.CreateAsync(user, password);
        Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(e => e.Description)));

        if (!active)
        {
            user.Deactivate();
            await userManager.UpdateAsync(user);
        }

        return (user, password);
    }

    public static HttpClient CreateClientWithCookies(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(_ => { }).CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            // The session and CSRF cookies are Secure (docs/08 §3). WebApplicationFactory's
            // default http:// base address makes TestServer report Request.IsHttps = false, so
            // .NET's CookieContainer correctly-per-RFC-6265 refuses to ever resend a Secure
            // cookie on a later request. An https:// base address makes TestServer synthesize
            // Scheme=https for these in-process requests, matching real production TLS
            // termination.
            BaseAddress = new Uri("https://localhost"),
        });

    /// <summary>Fetches a fresh CSRF token (docs/08 §4) and attaches it before every mutating
    /// call, exactly as the real frontend's central API client does once at boot.</summary>
    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object payload)
    {
        using HttpResponseMessage tokenResponse = await client.GetAsync("/api/v1/auth/csrf-token");
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        string csrfToken = tokenBody!["csrfToken"];

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        return await client.SendAsync(request);
    }

    /// <summary>Overwrites the stored hash via raw SQL to a known value (the entity has no
    /// public setter, by design - docs/08 §2 point 2) so a test can assert against a specific
    /// code without weakening the production hashing path.</summary>
    public static async Task<string> ForceKnownOtpAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var otpService = scope.ServiceProvider.GetRequiredService<IOtpService>();

        // IgnoreQueryFilters: this bare service scope has no authenticated CompanyId.
        PasswordResetOtp otp = await context.PasswordResetOtps
            .IgnoreQueryFilters()
            .Where(o => o.UserId == userId && o.ConsumedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstAsync();

        string code = otpService.GenerateCode();
        string hash = otpService.Hash(code);

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new NpgsqlCommand("UPDATE password_reset_otps SET otp_hash = @hash WHERE id = @id;", connection);
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("id", otp.Id);
        await command.ExecuteNonQueryAsync();

        return code;
    }
}
