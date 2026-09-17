using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Tasks 5.3, 5.10, 5.11, 5.14, and the item-specific slice of 5.16's acceptance list
/// (the 1,000-concurrent-allocation proof itself lives in
/// <see cref="DocumentSequenceServiceTests"/>, against the sequence engine directly - re-running
/// it at HTTP scale here would test the same guarantee twice, not a new one).</summary>
public sealed class ItemsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ItemsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, Guid CategoryId, Guid UnitId)> LoginAsOwnerWithMasterDataAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "قسم اختبار", description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage unitResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/units", new { nameArabic = "وحدة اختبار", abbreviation = (string?)null });
        var unit = await unitResponse.Content.ReadFromJsonAsync<IdDto>();

        return (client, category!.Id, unit!.Id);
    }

    [Fact]
    public async Task Creating_An_Item_Allocates_A_Six_Digit_ITM_Code()
    {
        (HttpClient client, Guid categoryId, Guid unitId) = await LoginAsOwnerWithMasterDataAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items",
            new { nameArabic = "لحم بقري", categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<ItemDto>();
        Assert.Matches("^ITM-\\d{6}$", item!.GeneratedCode);
    }

    /// <summary>docs/28 §5.1 point 2 / AC-28-2: submitting `generatedCode` is an explicit 400,
    /// not a silently-dropped field.</summary>
    [Fact]
    public async Task Submitting_A_Client_Supplied_GeneratedCode_Is_Rejected_With_400()
    {
        (HttpClient client, Guid categoryId, Guid unitId) = await LoginAsOwnerWithMasterDataAsync();

        using HttpResponseMessage tokenResponse = await client.GetAsync("/api/v1/auth/csrf-token");
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        var payload = new
        {
            nameArabic = "صنف بكود ممنوع",
            categoryId,
            baseUnitId = unitId,
            purchaseUnitId = (Guid?)null,
            defaultSupplierId = (Guid?)null,
            description = (string?)null,
            generatedCode = "ITM-999999",
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/items")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenBody!["csrfToken"]);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("GENERATED_FIELD_NOT_ACCEPTED", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creating_An_Item_With_A_Duplicate_Normalized_Name_Returns_409()
    {
        (HttpClient client, Guid categoryId, Guid unitId) = await LoginAsOwnerWithMasterDataAsync();
        string name = "صنف مكرر " + Guid.NewGuid().ToString("N")[..6];

        using HttpResponseMessage first = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items", new { nameArabic = name, categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using HttpResponseMessage second = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items", new { nameArabic = name, categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        string body = await second.Content.ReadAsStringAsync();
        Assert.Contains("DUPLICATE_ITEM_NAME", body, StringComparison.Ordinal);
    }

    /// <summary>docs/31 §4.2: a search term with different diacritics/alef forms than the
    /// stored name still matches, because both sides compare on NameNormalized.</summary>
    [Fact]
    public async Task Searching_By_A_Differently_Diacritized_Query_Still_Finds_The_Item()
    {
        (HttpClient client, Guid categoryId, Guid unitId) = await LoginAsOwnerWithMasterDataAsync();
        string uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        string storedName = $"مُحَمَّص {uniqueSuffix}";
        string searchTerm = $"محمص {uniqueSuffix}";

        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items", new { nameArabic = storedName, categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using HttpResponseMessage searchResponse = await client.GetAsync($"/api/v1/items?q={Uri.EscapeDataString(searchTerm)}");
        var page = await searchResponse.Content.ReadFromJsonAsync<PageDto>();

        Assert.Contains(page!.Items, i => i.NameArabic == storedName);
    }

    /// <summary>ADR-023, task 5.11: no `stock_ledger` row can exist before Phase 7, so the base
    /// unit is always changeable today - the guard itself is proven directly at the domain/
    /// service level once Phase 7 introduces posted ledger rows to check against.</summary>
    [Fact]
    public async Task Changing_The_Base_Unit_Succeeds_While_No_Ledger_Activity_Exists()
    {
        (HttpClient client, Guid categoryId, Guid unitId) = await LoginAsOwnerWithMasterDataAsync();
        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items", new { nameArabic = "صنف لتغيير الوحدة", categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        var item = await created.Content.ReadFromJsonAsync<ItemDto>();

        using HttpResponseMessage newUnitResponse = await AuthTestHelpers.PostJsonAsync(client, "/api/v1/units", new { nameArabic = "وحدة جديدة", abbreviation = (string?)null });
        var newUnit = await newUnitResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage changed = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/items/{item!.Id}/base-unit", new { baseUnitId = newUnit!.Id });

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var changedItem = await changed.Content.ReadFromJsonAsync<ItemDto>();
        Assert.Equal(newUnit.Id, changedItem!.BaseUnitId);
    }

    /// <summary>A lighter integration-level concurrency check (not the full 1,000-scale AC-28-1
    /// proof, already established against the sequence engine directly) confirming the
    /// category/unit-existence validation and the explicit transaction wrapper introduce no
    /// regression under concurrent item creation through the real service.</summary>
    [Fact]
    public async Task Fifty_Concurrent_Item_Creations_Yield_Fifty_Distinct_Codes()
    {
        (HttpClient client, Guid categoryId, Guid unitId) = await LoginAsOwnerWithMasterDataAsync();

        var tasks = new Task<HttpResponseMessage>[50];
        for (int i = 0; i < tasks.Length; i++)
        {
            int index = i;
            tasks[i] = AuthTestHelpers.PostJsonAsync(
                client, "/api/v1/items",
                new { nameArabic = $"صنف تزامني {index}-{Guid.NewGuid():N}", categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        }

        HttpResponseMessage[] responses = await Task.WhenAll(tasks);
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

            var codes = new List<string>();
            foreach (HttpResponseMessage response in responses)
            {
                var item = await response.Content.ReadFromJsonAsync<ItemDto>();
                codes.Add(item!.GeneratedCode);
            }

            Assert.Equal(50, codes.Distinct(StringComparer.Ordinal).Count());
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, string GeneratedCode, string NameArabic, Guid CategoryId, Guid BaseUnitId, bool IsActive);
    private sealed record PageDto(List<ItemDto> Items, string? NextCursor);
}
