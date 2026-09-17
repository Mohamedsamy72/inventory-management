using System.Net;
using System.Net.Http.Json;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 3.14 - the full mobile+password/OTP authentication flow over a real HTTP
/// pipeline, exercising the actual cookie and CSRF machinery rather than a mock. Rate-limit and
/// lockout tests live separately in <see cref="AuthRateLimitingTests"/> - see
/// <see cref="AuthTestHelpers"/>'s remarks for why.</summary>
public sealed class AuthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>docs/08 §3's `__Host-InventorySession` + unconditional `Secure` is the
    /// Production/Staging shape. `WebApplicationFactory` always runs as Development, which - per
    /// a real-browser finding during Phase F2 - deliberately uses a plain, non-`Secure` cookie
    /// name instead: a `__Host-` prefixed cookie is rejected by every real browser unless it also
    /// carries `Secure`, and `Secure` cookies require an actual HTTPS context to be stored at
    /// all, which docs/19 §1's `http://localhost:5165` local dev is not. This test therefore
    /// asserts the Development-specific flags, not the Production ones - see
    /// `DependencyInjection.AddAuthentication`'s remarks for the full story.</summary>
    [Fact]
    public async Task Valid_Login_Succeeds_And_Issues_The_Session_Cookie_With_The_Development_Flags()
    {
        (Domain.Entities.User user, string password) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies), "Expected a Set-Cookie header.");
        string cookieHeader = string.Join(";", cookies!);
        Assert.Contains("InventorySession", cookieHeader, StringComparison.Ordinal);
        Assert.DoesNotContain("__Host-InventorySession", cookieHeader, StringComparison.Ordinal);
        Assert.Contains("httponly", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookieHeader, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookieHeader, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invalid_Password_Returns_401_Invalid_Credentials()
    {
        (Domain.Entities.User user, _) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_CREDENTIALS", body, StringComparison.Ordinal);
        Assert.Contains("messageAr", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_Mobile_Number_Also_Returns_401_Invalid_Credentials()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client,
            "/api/v1/auth/login",
            new { mobileNumber = "01099999999" + Guid.NewGuid().ToString("N")[..4], password = "whatever" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_Account_Returns_403_Account_Inactive()
    {
        (Domain.Entities.User user, string password) = await AuthTestHelpers.CreateActiveUserAsync(_factory, active: false);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ACCOUNT_INACTIVE", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Csrf_Token_Endpoint_Returns_A_Token_And_Sets_A_Pairing_Cookie()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await client.GetAsync("/api/v1/auth/csrf-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.True(body!.ContainsKey("csrfToken"));
        Assert.False(string.IsNullOrWhiteSpace(body["csrfToken"]));

        // Development's plain "InventoryCsrf" name, not Production's "__Host-InventoryCsrf" -
        // same reasoning as the session cookie above.
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.Contains("InventoryCsrf", StringComparison.Ordinal));
        Assert.DoesNotContain(cookies!, c => c.Contains("__Host-InventoryCsrf", StringComparison.Ordinal));
    }

    /// <summary>Task 3.14's explicit "CSRF rejection" case: a mutating request presenting no
    /// X-CSRF-TOKEN header at all must be rejected, even with valid credentials in the body.</summary>
    [Fact]
    public async Task Login_Without_A_Csrf_Token_Is_Rejected()
    {
        (Domain.Entities.User user, string password) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Otp_Request_Always_Returns_200_Whether_Or_Not_The_Mobile_Exists()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);
        string unknownMobile = "01098" + Guid.NewGuid().ToString("N")[..5];

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/forgot-password/otp", new { mobileNumber = unknownMobile });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Otp_Verify_With_Wrong_Code_Returns_400_Otp_Invalid()
    {
        (Domain.Entities.User user, _) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage otpResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/forgot-password/otp", new { mobileNumber = user.MobileNumber });
        Assert.Equal(HttpStatusCode.OK, otpResponse.StatusCode);

        using HttpResponseMessage verifyResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/forgot-password/verify", new { mobileNumber = user.MobileNumber, code = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
        string body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("OTP_INVALID", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Otp_Full_Reset_Flow_Succeeds_With_The_Real_Code_And_The_Code_Cannot_Be_Reused()
    {
        (Domain.Entities.User user, _) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/forgot-password/otp", new { mobileNumber = user.MobileNumber });
        string code = await AuthTestHelpers.ForceKnownOtpAsync(_factory, user.Id);

        using HttpResponseMessage verifyResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/forgot-password/verify", new { mobileNumber = user.MobileNumber, code });
        Assert.True(verifyResponse.StatusCode == HttpStatusCode.OK, await verifyResponse.Content.ReadAsStringAsync());

        var verifyBody = await verifyResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        string resetToken = verifyBody!["resetToken"];

        using HttpResponseMessage resetResponse = await AuthTestHelpers.PostJsonAsync(
            client,
            "/api/v1/auth/forgot-password/reset",
            new { mobileNumber = user.MobileNumber, resetToken, newPassword = "NewStr0ng!Passw0rd" });
        Assert.True(resetResponse.StatusCode == HttpStatusCode.OK, await resetResponse.Content.ReadAsStringAsync());

        // The code was single-use (consumed at verify) - reusing it must fail even though the
        // hash briefly matched a real row.
        using HttpResponseMessage reuseResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/forgot-password/verify", new { mobileNumber = user.MobileNumber, code });
        Assert.Equal(HttpStatusCode.BadRequest, reuseResponse.StatusCode);

        // The new password actually works.
        using HttpResponseMessage loginResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password = "NewStr0ng!Passw0rd" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Otp_Expiry_Is_Rejected_Even_With_The_Correct_Code()
    {
        (Domain.Entities.User user, _) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/forgot-password/otp", new { mobileNumber = user.MobileNumber });
        string code = await AuthTestHelpers.ForceKnownOtpAsync(_factory, user.Id);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = new NpgsqlCommand(
                "UPDATE password_reset_otps SET expires_at = now() - interval '1 minute' WHERE user_id = @userId AND consumed_at IS NULL;",
                connection);
            command.Parameters.AddWithValue("userId", user.Id);
            await command.ExecuteNonQueryAsync();
        }

        using HttpResponseMessage verifyResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/auth/forgot-password/verify", new { mobileNumber = user.MobileNumber, code });

        Assert.Equal(HttpStatusCode.BadRequest, verifyResponse.StatusCode);
        string body = await verifyResponse.Content.ReadAsStringAsync();
        Assert.Contains("OTP_EXPIRED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_Then_Account_Me_Is_Unauthenticated()
    {
        (Domain.Entities.User user, string password) = await AuthTestHelpers.CreateActiveUserAsync(_factory);
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password });
        using HttpResponseMessage meBeforeLogout = await client.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.OK, meBeforeLogout.StatusCode);

        using HttpResponseMessage logoutResponse = await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        using HttpResponseMessage meAfterLogout = await client.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Account_Me_Returns_Identity_Role_Scopes_And_Permissions()
    {
        (Domain.Entities.User user, string password) = await AuthTestHelpers.CreateActiveUserAsync(_factory);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            Role ownerRole = await context.Roles.AsNoTracking().FirstAsync(r => r.Name == Domain.Enums.RoleName.Owner);
            context.UserRoles.Add(new UserRole(user.Id, ownerRole.Id));
            await context.SaveChangesAsync();
        }

        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);
        await AuthTestHelpers.PostJsonAsync(client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password });

        using HttpResponseMessage response = await client.GetAsync("/api/v1/account/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains(user.MobileNumber, body, StringComparison.Ordinal);
        Assert.Contains("Owner", body, StringComparison.Ordinal);
        Assert.Contains("items:view", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Account_Me_Without_A_Session_Is_Unauthorized()
    {
        using HttpClient client = AuthTestHelpers.CreateClientWithCookies(_factory);

        using HttpResponseMessage response = await client.GetAsync("/api/v1/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Proves the OTHER branch of the Development/Production cookie split
    /// (`DependencyInjection.AddAuthentication`'s remarks): a Production-mode host still gets the
    /// strict docs/08 §3 shape - `__Host-` prefixed, unconditionally `Secure`.</summary>
    [Fact]
    public async Task Production_Environment_Still_Issues_The_Strict_Host_Prefixed_Secure_Cookie()
    {
        (Domain.Entities.User user, string password) = await AuthTestHelpers.CreateActiveUserAsync(_factory);

        // "Production" skips appsettings.Development.json, which is the only file carrying a
        // real connection string - re-supply it explicitly so this test's login can still reach
        // the same database Development already seeded the user into, while still exercising
        // the actual Production cookie-policy code path.
        //
        // This MUST be an environment variable, not a WithWebHostBuilder ConfigureAppConfiguration
        // override: Program.cs calls AddInfrastructure(builder.Configuration, ...) - which reads
        // and captures the connection string into AddDbContext's closure - as a top-level statement
        // that runs as part of WebApplicationFactory's deferred host-build interception, BEFORE any
        // WithWebHostBuilder configuration delta is merged in. An env var, by contrast, is loaded by
        // WebApplication.CreateBuilder() itself, ahead of that statement, so it is visible in time.
        using IServiceScope devScope = _factory.Services.CreateScope();
        var devConfiguration = devScope.ServiceProvider.GetRequiredService<IConfiguration>();
        string connectionString = devConfiguration.GetConnectionString(Inventory.Infrastructure.DependencyInjection.DatabaseConnectionName)!;

        const string ConnectionStringEnvironmentVariable = "ConnectionStrings__InventoryDatabase";
        Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, connectionString);
        try
        {
            using WebApplicationFactory<Program> productionFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
            });

            // Same BaseAddress workaround as AuthTestHelpers.CreateClientWithCookies: TestServer
            // reports Request.IsHttps = false over the default http:// base address, so a real
            // Secure cookie (correctly) never gets resent - an https:// base address makes it
            // synthesize Scheme=https instead, matching real production TLS termination.
            using HttpClient client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                BaseAddress = new Uri("https://localhost"),
            });

            using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
                client, "/api/v1/auth/login", new { mobileNumber = user.MobileNumber, password });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
            string cookieHeader = string.Join(";", cookies!);
            Assert.Contains("__Host-InventorySession", cookieHeader, StringComparison.Ordinal);
            Assert.Contains("secure", cookieHeader, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvironmentVariable, null);
        }
    }
}
