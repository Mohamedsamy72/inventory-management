using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Change 4 - Owner/Admin direct issue ("أمر صرف"): POST /api/v1/supplies/direct-issue,
/// gated by the Owner/Admin-only <c>supplies:direct-issue</c> permission. One atomic backend
/// operation - NOT a browser-side chain of request/approve/confirm calls.</summary>
public sealed class DirectIssueTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DirectIssueTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record SupplyLineDto(Guid Id, Guid ItemId, decimal DispatchedQuantity, decimal? ReceivedQuantity, Guid UnitId);
    private sealed record SupplyDto(Guid Id, string DocumentNumber, Guid WarehouseId, Guid RestaurantId, Guid? SupplyRequestId, SupplyStatus Status, List<SupplyLineDto> Lines);
    private sealed record OperationDto(SupplyDto Supply);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid RestaurantId, Guid WarehouseId, Guid ItemId, Guid BaseUnitId);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<Seed> SeedAsync(decimal openingBalance)
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
                client, "/api/v1/receiving-orders",
                new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
            var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();
            await AuthTestHelpers.PostJsonAsync(
                client, $"/api/v1/receiving-orders/{order!.Id}/lines",
                new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = openingBalance, unitCost = 10m, notes = (string?)null });
            using HttpResponseMessage submitResponse = await SendIdempotentAsync(client, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        }

        return seed;
    }

    private static async Task<HttpResponseMessage> SendIdempotentAsync(HttpClient client, string url, object payload, string idempotencyKey)
    {
        using HttpResponseMessage tokenResponse = await client.GetAsync("/api/v1/auth/csrf-token");
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenBody!["csrfToken"]);
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> DirectIssueAsync(HttpClient client, Guid warehouseId, Guid restaurantId, Seed seed, decimal quantity, string? key = null) =>
        SendIdempotentAsync(client, "/api/v1/supplies/direct-issue",
            new { warehouseId, restaurantId, lines = new[] { new { itemId = seed.ItemId, unitId = seed.BaseUnitId, quantity } } },
            key ?? Guid.NewGuid().ToString());

    private async Task<HttpClient> ClientForRoleAsync(Seed seed, RoleName role)
    {
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        return await AuthTestHelpers.LoginAsAsync(_factory, user, password);
    }

    private async Task<decimal> BalanceAsync(Guid warehouseId, Guid itemId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        StockBalance? balance = await context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
        return balance?.Quantity ?? 0m;
    }

    private async Task<int> LedgerCountAsync(Guid companyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == companyId);
    }

    [Fact]
    public async Task Owner_Direct_Issue_Deducts_Stock_Posts_One_Ledger_Row_Audits_And_Exposes_No_Financial_Fields()
    {
        Seed seed = await SeedAsync(openingBalance: 100m);
        int ledgerBefore = await LedgerCountAsync(seed.CompanyId);

        using HttpResponseMessage response = await DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 40m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string raw = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<OperationDto>(raw, JsonOptions);
        Assert.Equal(SupplyStatus.Confirmed, body!.Supply.Status);
        Assert.Null(body.Supply.SupplyRequestId);
        Assert.Equal(40m, body.Supply.Lines.Single().ReceivedQuantity);
        Assert.DoesNotContain("cost", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("price", raw, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(60m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
        Assert.Equal(ledgerBefore + 1, await LedgerCountAsync(seed.CompanyId));

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.True(await context.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == seed.CompanyId && a.Action == "SUPPLY_DIRECT_ISSUED" && a.EntityId == body.Supply.Id));
    }

    [Fact]
    public async Task Admin_Can_Direct_Issue()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        using HttpClient admin = await ClientForRoleAsync(seed, RoleName.Admin);

        using HttpResponseMessage response = await DirectIssueAsync(admin, seed.WarehouseId, seed.RestaurantId, seed, 10m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(40m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
    }

    [Theory]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.RestaurantSupervisor)]
    [InlineData(RoleName.User)]
    public async Task Non_Owner_Admin_Roles_Are_Forbidden_And_Nothing_Is_Written(RoleName role)
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        int ledgerBefore = await LedgerCountAsync(seed.CompanyId);
        using HttpClient client = await ClientForRoleAsync(seed, role);

        using HttpResponseMessage response = await DirectIssueAsync(client, seed.WarehouseId, seed.RestaurantId, seed, 10m);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(50m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
        Assert.Equal(ledgerBefore, await LedgerCountAsync(seed.CompanyId));
    }

    [Fact]
    public async Task Insufficient_Stock_Is_Rejected_With_The_Standard_Error_And_Nothing_Is_Written()
    {
        Seed seed = await SeedAsync(openingBalance: 60m);
        int ledgerBefore = await LedgerCountAsync(seed.CompanyId);

        using HttpResponseMessage response = await DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 100m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INSUFFICIENT_STOCK", body, StringComparison.Ordinal);
        Assert.Contains("messageAr", body, StringComparison.Ordinal);
        Assert.Equal(60m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
        Assert.Equal(ledgerBefore, await LedgerCountAsync(seed.CompanyId));

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.False(await context.Supplies.IgnoreQueryFilters().AnyAsync(s => s.CompanyId == seed.CompanyId));
    }

    [Fact]
    public async Task Another_Companys_Warehouse_Or_Restaurant_Is_Rejected()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        Seed other = await SeedAsync(openingBalance: 50m);

        using HttpResponseMessage foreignWarehouse = await DirectIssueAsync(seed.OwnerClient, other.WarehouseId, seed.RestaurantId, seed, 10m);
        Assert.Equal(HttpStatusCode.Conflict, foreignWarehouse.StatusCode);

        using HttpResponseMessage foreignRestaurant = await DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, other.RestaurantId, seed, 10m);
        Assert.Equal(HttpStatusCode.NotFound, foreignRestaurant.StatusCode);

        Assert.Equal(50m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
        Assert.Equal(50m, await BalanceAsync(other.WarehouseId, other.ItemId));
    }

    [Fact]
    public async Task Replaying_The_Same_Idempotency_Key_Deducts_Once()
    {
        Seed seed = await SeedAsync(openingBalance: 100m);
        string key = Guid.NewGuid().ToString();

        using HttpResponseMessage first = await DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 30m, key);
        using HttpResponseMessage replay = await DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 30m, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.True(replay.IsSuccessStatusCode);
        Assert.Equal(70m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
    }

    [Fact]
    public async Task Two_Concurrent_Direct_Issues_Of_Sixty_From_A_Hundred_Never_Oversell()
    {
        Seed seed = await SeedAsync(openingBalance: 100m);

        HttpResponseMessage[] responses = await Task.WhenAll(
            DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 60m),
            DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 60m));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(40m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
        foreach (HttpResponseMessage r in responses)
        {
            r.Dispose();
        }
    }

    [Fact]
    public async Task Non_Positive_Quantity_Is_Rejected()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);

        using HttpResponseMessage zero = await DirectIssueAsync(seed.OwnerClient, seed.WarehouseId, seed.RestaurantId, seed, 0m);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(50m, await BalanceAsync(seed.WarehouseId, seed.ItemId));
    }
}
