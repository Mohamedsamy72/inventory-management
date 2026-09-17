using System.Net;
using System.Net.Http.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 5.5, and the pattern every other Phase 5 master-data resource follows: create,
/// duplicate-name rejection, keyset pagination, update, deactivate/reactivate, permission
/// gating, and tenant isolation.</summary>
public sealed class CategoriesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CategoriesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Company Company)> LoginAsOwnerAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);
        return (client, company);
    }

    /// <summary>Every Phase 5 mutating endpoint must carry the same CSRF protection Phase 3
    /// established for auth (task 3.7) - proven once here rather than per resource, since all
    /// six master-data resources apply the identical AntiforgeryEndpointFilter.</summary>
    [Fact]
    public async Task Creating_A_Category_Without_A_Csrf_Token_Is_Rejected()
    {
        (HttpClient client, _) = await LoginAsOwnerAsync();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/categories", new { nameArabic = "قسم بلا حماية", description = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Creating_A_Category_Succeeds_And_Returns_It()
    {
        (HttpClient client, _) = await LoginAsOwnerAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "مشروبات", description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.Equal("مشروبات", body!.NameArabic);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Creating_A_Duplicate_Category_Name_In_The_Same_Company_Returns_409()
    {
        (HttpClient client, _) = await LoginAsOwnerAsync();
        string name = "قسم مكرر " + Guid.NewGuid().ToString("N")[..6];

        using HttpResponseMessage first = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = name, description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage second = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = name, description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        string body = await second.Content.ReadAsStringAsync();
        Assert.Contains("DUPLICATE_NAME", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deactivating_Then_Reactivating_A_Category_Round_Trips_Correctly()
    {
        (HttpClient client, _) = await LoginAsOwnerAsync();
        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "أجبان", description = (string?)null });
        var category = await created.Content.ReadFromJsonAsync<CategoryDto>();

        using HttpResponseMessage deactivated = await AuthTestHelpers.PostJsonAsync(
            client, $"/api/v1/categories/{category!.Id}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var deactivatedBody = await deactivated.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.False(deactivatedBody!.IsActive);

        using HttpResponseMessage reactivated = await AuthTestHelpers.PostJsonAsync(
            client, $"/api/v1/categories/{category.Id}/reactivate", new { });
        var reactivatedBody = await reactivated.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.True(reactivatedBody!.IsActive);
    }

    [Fact]
    public async Task Listing_Categories_Paginates_By_Keyset_With_A_Working_Next_Cursor()
    {
        (HttpClient client, _) = await LoginAsOwnerAsync();
        for (int i = 0; i < 5; i++)
        {
            using HttpResponseMessage r = await AuthTestHelpers.PostJsonAsync(
                client, "/api/v1/categories", new { nameArabic = $"قسم-{Guid.NewGuid():N}", description = (string?)null });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        using HttpResponseMessage firstPage = await client.GetAsync("/api/v1/categories?limit=3");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        var page1 = await firstPage.Content.ReadFromJsonAsync<PageDto>();
        Assert.Equal(3, page1!.Items.Count);
        Assert.False(string.IsNullOrEmpty(page1.NextCursor));

        using HttpResponseMessage secondPage = await client.GetAsync($"/api/v1/categories?limit=3&cursor={Uri.EscapeDataString(page1.NextCursor!)}");
        var page2 = await secondPage.Content.ReadFromJsonAsync<PageDto>();
        Assert.True(page2!.Items.Count >= 2);

        var page1Ids = page1.Items.Select(i => i.Id).ToHashSet();
        var page2Ids = page2.Items.Select(i => i.Id).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));
    }

    [Fact]
    public async Task A_User_Without_Categories_Manage_Is_Forbidden()
    {
        (HttpClient ownerClient, Company company) = await LoginAsOwnerAsync();
        (User plainUser, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, plainUser.Id, RoleName.User);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, plainUser, password);

        using HttpResponseMessage response = await client.GetAsync("/api/v1/categories");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Getting_A_Category_From_A_Different_Company_Returns_404()
    {
        (HttpClient ownerAClient, _) = await LoginAsOwnerAsync();
        (HttpClient ownerBClient, _) = await LoginAsOwnerAsync();

        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            ownerAClient, "/api/v1/categories", new { nameArabic = "قسم شركة أ", description = (string?)null });
        var category = await created.Content.ReadFromJsonAsync<CategoryDto>();

        using HttpResponseMessage response = await ownerBClient.GetAsync($"/api/v1/categories/{category!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record CategoryDto(Guid Id, string NameArabic, string? Description, bool IsActive, DateTimeOffset CreatedAt);
    private sealed record PageDto(List<CategoryDto> Items, string? NextCursor);
}
