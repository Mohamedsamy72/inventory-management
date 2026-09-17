using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Phase S1 - Security Hardening & Adversarial Regression (docs/09 §S1). Not a
/// re-verification of every phase's own scope/tenant/mass-assignment tests (already covered
/// individually across Phases 4-14's own suites) - this file covers the cross-cutting controls
/// that have no single phase to live in: security headers, U+202E stripping, `messageEn`
/// suppression in Production, and the absence of any raw stock-mutation escape hatch.</summary>
public sealed class SecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);

    [Fact]
    public async Task Every_Response_Carries_The_Baseline_Security_Headers()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    /// <summary>The same headers must also appear on an ERROR response - a request that never
    /// authenticates is exactly the kind of hostile traffic these headers protect against.</summary>
    [Fact]
    public async Task Security_Headers_Are_Present_On_An_Unauthenticated_403_Too()
    {
        using HttpClient client = _factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/api/v1/audit");

        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
    }

    /// <summary>AC-31-9 (docs/31 §7): stripped before storage, not merely before display -
    /// asserted directly against the persisted row, not the response.</summary>
    [Fact]
    public async Task Right_To_Left_Override_Is_Stripped_Before_Storage()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        string maliciousName = "قسم‮مخفي";
        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = maliciousName, description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<IdDto>();

        using IServiceScope verifyScope = _factory.Services.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Category persisted = await verifyContext.Categories.IgnoreQueryFilters().AsNoTracking().FirstAsync(c => c.Id == created!.Id);

        Assert.DoesNotContain('‮', persisted.NameArabic);
        Assert.Equal("قسممخفي", persisted.NameArabic);
    }

    [Fact]
    public async Task No_Raw_Stock_Mutation_Endpoint_Exists()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage patchResponse = await client.PatchAsync("/api/v1/stock-balances", JsonContent.Create(new { quantity = 999m }));
        Assert.True(patchResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

        using HttpResponseMessage postResponse = await client.PostAsync("/api/v1/stock-ledger", JsonContent.Create(new { }));
        Assert.True(postResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    /// <summary>CR-072 (docs/31 §4.1): the developer-only `messageEn` diagnostic must never reach
    /// a Production response.</summary>
    [Fact]
    public async Task MessageEn_Is_Suppressed_In_Production()
    {
        using WebApplicationFactory<Program> productionFactory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using HttpClient client = productionFactory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { mobileNumber = "0100000000", password = "wrong" });

        string body = await response.Content.ReadAsStringAsync();
        using JsonDocument document = JsonDocument.Parse(body);

        Assert.True(document.RootElement.TryGetProperty("messageAr", out _));
        Assert.False(document.RootElement.TryGetProperty("messageEn", out _));
    }

    // docs/09 §S1's `dotnet list package --vulnerable` check is deliberately NOT an automated
    // test here: it needs a live NuGet feed and took over 15 minutes on this machine, which
    // would make every routine `dotnet test` run prohibitively slow. Run manually (see docs/27's
    // Phase S1 ledger for the last recorded result and the exact command) as part of a release
    // check instead, per docs/09 Phase R1's own "production deployment rehearsal" step.
}
