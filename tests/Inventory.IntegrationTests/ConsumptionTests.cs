using System.Net;
using System.Net.Http.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>REQ-07 (docs/26, docs/04 §12) - restaurant consumption logging, a Phase T1
/// traceability gap: the entity/table were built in Phase 2 but never scheduled by name in
/// docs/09. Statistical only - the defining invariant is that it NEVER touches warehouse
/// stock.</summary>
public sealed class ConsumptionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConsumptionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record ConsumptionDto(Guid Id, Guid RestaurantId, Guid ItemId, decimal Quantity, Guid UnitId, DateOnly ConsumptionDate, Guid RecordedBy, string? Notes, DateTimeOffset CreatedAt);
    private sealed record PageDto<T>(List<T> Items, string? NextCursor);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid RestaurantId, Guid WarehouseId, Guid ItemId, Guid BaseUnitId);

    private async Task<Seed> SeedAsync(decimal openingBalance = 100m)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage warehouseResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses", new { nameArabic = "مستودع", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants", new { nameArabic = "فرع", code = "BR-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var restaurant = await restaurantResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "قسم " + Guid.NewGuid().ToString("N")[..6], description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage unitResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/units", new { nameArabic = "وحدة " + Guid.NewGuid().ToString("N")[..6], abbreviation = (string?)null });
        var unit = await unitResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage itemResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items",
            new { nameArabic = "صنف " + Guid.NewGuid().ToString("N")[..6], categoryId = category!.Id, baseUnitId = unit!.Id, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>();

        var seed = new Seed(company.Id, client, restaurant!.Id, warehouse!.Id, item!.Id, item.BaseUnitId);

        if (openingBalance > 0)
        {
            using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
                seed.OwnerClient, "/api/v1/receiving-orders",
                new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
            var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

            await AuthTestHelpers.PostJsonAsync(
                seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
                new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = openingBalance, unitCost = 10m, notes = (string?)null });

            using HttpResponseMessage tokenResponse = await seed.OwnerClient.GetAsync("/api/v1/auth/csrf-token");
            var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/receiving-orders/{order.Id}/submit")
            {
                Content = JsonContent.Create(new { }),
            };
            request.Headers.Add("X-CSRF-TOKEN", tokenBody!["csrfToken"]);
            request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
            await seed.OwnerClient.SendAsync(request);
        }

        return seed;
    }

    private async Task<decimal> GetBalanceAsync(Guid warehouseId, Guid itemId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        StockBalance? balance = await context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
        return balance?.Quantity ?? 0m;
    }

    [Fact]
    public async Task Owner_Can_Record_And_List_Consumption_With_Zero_Stock_Effect()
    {
        Seed seed = await SeedAsync();
        decimal balanceBefore = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);

        using HttpResponseMessage recordResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/consumption",
            new { restaurantId = seed.RestaurantId, itemId = seed.ItemId, quantity = 5m, unitId = seed.BaseUnitId, consumptionDate = DateOnly.FromDateTime(DateTime.UtcNow), notes = "استهلاك يومي" });
        Assert.Equal(HttpStatusCode.Created, recordResponse.StatusCode);
        var recorded = await recordResponse.Content.ReadFromJsonAsync<ConsumptionDto>();
        Assert.Equal(5m, recorded!.Quantity);

        decimal balanceAfter = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(balanceBefore, balanceAfter);

        using HttpResponseMessage listResponse = await seed.OwnerClient.GetAsync($"/api/v1/consumption?restaurantId={seed.RestaurantId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<ConsumptionDto>>();
        Assert.Contains(page!.Items, c => c.Id == recorded.Id);
    }

    [Fact]
    public async Task Restaurant_Supervisor_Can_Log_For_Their_Own_Restaurant_Only()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage otherRestaurantResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/restaurants", new { nameArabic = "فرع آخر", code = "BR-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var otherRestaurant = await otherRestaurantResponse.Content.ReadFromJsonAsync<IdDto>();

        (User supervisorUser, string supervisorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisorUser.Id, RoleName.RestaurantSupervisor);
        HttpClient supervisorClient = await AuthTestHelpers.LoginAsAsync(_factory, supervisorUser, supervisorPassword);

        using HttpResponseMessage scopeResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Put, $"/api/v1/users/{supervisorUser.Id}/scope",
            new { warehouseIds = Array.Empty<Guid>(), restaurantIds = new[] { seed.RestaurantId } });
        Assert.Equal(HttpStatusCode.OK, scopeResponse.StatusCode);

        using HttpResponseMessage forbiddenResponse = await AuthTestHelpers.PostJsonAsync(
            supervisorClient, "/api/v1/consumption",
            new { restaurantId = otherRestaurant!.Id, itemId = seed.ItemId, quantity = 2m, unitId = seed.BaseUnitId, consumptionDate = DateOnly.FromDateTime(DateTime.UtcNow), notes = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using HttpResponseMessage allowedResponse = await AuthTestHelpers.PostJsonAsync(
            supervisorClient, "/api/v1/consumption",
            new { restaurantId = seed.RestaurantId, itemId = seed.ItemId, quantity = 2m, unitId = seed.BaseUnitId, consumptionDate = DateOnly.FromDateTime(DateTime.UtcNow), notes = (string?)null });
        Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
    }

    [Fact]
    public async Task Warehouse_Staff_Cannot_Log_Consumption()
    {
        Seed seed = await SeedAsync();

        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            staffClient, "/api/v1/consumption",
            new { restaurantId = seed.RestaurantId, itemId = seed.ItemId, quantity = 1m, unitId = seed.BaseUnitId, consumptionDate = DateOnly.FromDateTime(DateTime.UtcNow), notes = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
