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

/// <summary>Phase 11 - Restaurant Receipt Confirmation (docs/09 tasks 11.1-11.11, docs/30 §7).
/// The ONLY stock-deducting path in the product - the highest-consequence module in the plan.
/// docs/23 Scenarios 4-6 are this phase's own acceptance criteria.</summary>
public sealed class SupplyConfirmationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SupplyConfirmationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record ReqLineDto(Guid Id, Guid ItemId, decimal RequestedQuantity, decimal FulfilledQuantity, Guid UnitId, string? Notes);
    private sealed record RequestDto(Guid Id, string DocumentNumber, Guid RestaurantId, Guid WarehouseId, SupplyRequestStatus Status, Guid RequestedBy, DateTimeOffset RequestedAt, List<ReqLineDto> Lines);
    private sealed record SupplyLineDto(Guid Id, Guid ItemId, decimal DispatchedQuantity, decimal? ReceivedQuantity, Guid UnitId);
    private sealed record SupplyDto(Guid Id, string DocumentNumber, Guid WarehouseId, Guid RestaurantId, Guid? SupplyRequestId, SupplyStatus Status, Guid? PreparedBy, DateTimeOffset? PreparedAt, Guid? DispatchedBy, DateTimeOffset? DispatchedAt, List<SupplyLineDto> Lines);
    private sealed record SupplyOperationDto(SupplyDto Supply, List<Guid> InsufficientStockItemIds);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid RestaurantId, Guid WarehouseId, Guid ItemId, Guid BaseUnitId);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<Seed> SeedAsync(decimal openingBalance, decimal openingCost = 10m)
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
            client, "/api/v1/restaurants", new { nameArabic = "فرع", code = "BR-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = warehouse!.Id, address = (string?)null, description = (string?)null });
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

        var seed = new Seed(company.Id, client, restaurant!.Id, warehouse.Id, item!.Id, item.BaseUnitId);

        if (openingBalance > 0)
        {
            await PostReceivingAsync(seed, openingBalance, openingCost);
        }

        return seed;
    }

    private static async Task PostReceivingAsync(Seed seed, decimal quantity, decimal unitCost)
    {
        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = quantity, unitCost, notes = (string?)null });

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendIdempotentAsync(HttpClient client, string url, object payload, string idempotencyKey)
    {
        using HttpResponseMessage tokenResponse = await client.GetAsync("/api/v1/auth/csrf-token");
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        string csrfToken = tokenBody!["csrfToken"];

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request);
    }

    /// <summary>Creates a request, submits it, fulfils it in full, and dispatches - returning the
    /// resulting Dispatched Supply, ready for confirmation.</summary>
    private static async Task<SupplyDto> CreateDispatchedSupplyAsync(Seed seed, decimal quantity)
    {
        using HttpResponseMessage createRequest = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId });
        var request = await createRequest.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request!.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = quantity, notes = (string?)null });

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        var submitted = await submitResponse.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);
        Guid lineId = submitted!.Lines.Single().Id;

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = quantity } } });
        var fulfillResult = await fulfillResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        using HttpResponseMessage dispatchResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{fulfillResult!.Supply.Id}/dispatch", new { });
        var dispatchResult = await dispatchResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        return dispatchResult!.Supply;
    }

    private async Task<decimal> GetBalanceAsync(Guid warehouseId, Guid itemId)
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
    public async Task Scenario_4_Full_Confirmation_Deducts_Exactly_The_Received_Quantity()
    {
        Seed seed = await SeedAsync(openingBalance: 580m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 20m);
        Guid lineId = supply.Lines.Single().Id;

        using HttpResponseMessage confirmResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 20m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var result = await confirmResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);
        Assert.Equal(SupplyStatus.Confirmed, result!.Supply.Status);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(560m, balance);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        bool ledgerRowExists = await context.StockLedgerEntries.IgnoreQueryFilters()
            .AnyAsync(l => l.ItemId == seed.ItemId && l.MovementType == MovementType.RestaurantReceiptConfirmed && l.BaseQuantity == -20m);
        Assert.True(ledgerRowExists);
    }

    [Fact]
    public async Task Scenario_5_Partial_Confirmation_Deducts_Received_Only_And_Logs_Discrepancy()
    {
        Seed seed = await SeedAsync(openingBalance: 580m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 20m);
        Guid lineId = supply.Lines.Single().Id;

        using HttpResponseMessage confirmResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 18m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var result = await confirmResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);
        Assert.Equal(SupplyStatus.ConfirmedWithDiscrepancy, result!.Supply.Status);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(562m, balance);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(d => d.ReferenceId == supply.Id && d.ItemId == seed.ItemId);
        Assert.Equal(-2m, discrepancy.Variance);
        Assert.Equal(DiscrepancyStatus.Open, discrepancy.Status);
        Assert.Equal(DiscrepancyType.SupplyReceiptVariance, discrepancy.Type);
    }

    [Fact]
    public async Task Complete_Rejection_Writes_No_Ledger_Row_But_Logs_A_Discrepancy()
    {
        Seed seed = await SeedAsync(openingBalance: 580m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 20m);
        Guid lineId = supply.Lines.Single().Id;
        int ledgerCountBefore = await LedgerCountAsync(seed.CompanyId);

        using HttpResponseMessage confirmResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 0m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var result = await confirmResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);
        Assert.Equal(SupplyStatus.RejectedAtDelivery, result!.Supply.Status);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(580m, balance);

        int ledgerCountAfter = await LedgerCountAsync(seed.CompanyId);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(d => d.ReferenceId == supply.Id && d.ItemId == seed.ItemId);
        Assert.Equal(-20m, discrepancy.Variance);
    }

    [Fact]
    public async Task Scenario_6_Exactly_One_Of_Two_Concurrent_Confirmations_Succeeds_Never_Negative()
    {
        Seed seed = await SeedAsync(openingBalance: 20m);
        SupplyDto supplyA = await CreateDispatchedSupplyAsync(seed, 15m);
        SupplyDto supplyB = await CreateDispatchedSupplyAsync(seed, 15m);

        Guid lineA = supplyA.Lines.Single().Id;
        Guid lineB = supplyB.Lines.Single().Id;

        Task<HttpResponseMessage> confirmA = SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supplyA.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineA, receivedQuantity = 15m } } }, Guid.NewGuid().ToString());
        Task<HttpResponseMessage> confirmB = SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supplyB.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineB, receivedQuantity = 15m } } }, Guid.NewGuid().ToString());

        HttpResponseMessage[] responses = await Task.WhenAll(confirmA, confirmB);
        try
        {
            int successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
            int failureCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);
            Assert.Equal(1, successCount);
            Assert.Equal(1, failureCount);

            decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
            Assert.Equal(5m, balance);
            Assert.True(balance >= 0m);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Confirmation_Against_A_Depleted_Balance_Writes_Nothing_And_Supply_Stays_Dispatched()
    {
        // docs/30 §7.1's "impossible confirmation": the supply was validly dispatched, but the
        // balance has since been depleted by an intervening event, so confirming it now must
        // roll back completely - the supply remains Dispatched, no ledger row, no status change.
        Seed seed = await SeedAsync(openingBalance: 5m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 5m);
        Guid lineId = supply.Lines.Single().Id;

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var connection = (Npgsql.NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = new Npgsql.NpgsqlCommand(
            "UPDATE stock_balances SET quantity = 0 WHERE warehouse_id = @wh AND item_id = @item;", connection);
        command.Parameters.AddWithValue("wh", seed.WarehouseId);
        command.Parameters.AddWithValue("item", seed.ItemId);
        await command.ExecuteNonQueryAsync();

        int ledgerCountBefore = await LedgerCountAsync(seed.CompanyId);

        using HttpResponseMessage failedConfirm = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 5m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, failedConfirm.StatusCode);

        int ledgerCountAfter = await LedgerCountAsync(seed.CompanyId);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);

        using IServiceScope scope2 = _factory.Services.CreateScope();
        var context2 = scope2.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Supply reloaded = await context2.Supplies.IgnoreQueryFilters().AsNoTracking().FirstAsync(s => s.Id == supply.Id);
        Assert.Equal(SupplyStatus.Dispatched, reloaded.Status);
    }

    [Fact]
    public async Task Idempotent_Replay_Deducts_Once()
    {
        Seed seed = await SeedAsync(openingBalance: 20m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 20m);
        Guid lineId = supply.Lines.Single().Id;
        string key = Guid.NewGuid().ToString();

        using HttpResponseMessage first = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 20m } } }, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using HttpResponseMessage replay = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 20m } } }, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(0m, balance);
    }

    [Fact]
    public async Task Second_Non_Replayed_Confirmation_Is_Already_Confirmed()
    {
        Seed seed = await SeedAsync(openingBalance: 20m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 20m);
        Guid lineId = supply.Lines.Single().Id;

        using HttpResponseMessage first = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 20m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using HttpResponseMessage second = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 20m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Cross_Branch_Confirmation_Is_Forbidden()
    {
        Seed seed = await SeedAsync(openingBalance: 20m);
        SupplyDto supply = await CreateDispatchedSupplyAsync(seed, 20m);
        Guid lineId = supply.Lines.Single().Id;

        (User supervisorUser, string supervisorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisorUser.Id, RoleName.RestaurantSupervisor);
        HttpClient supervisorClient = await AuthTestHelpers.LoginAsAsync(_factory, supervisorUser, supervisorPassword);
        // Deliberately no restaurant scope assigned.

        using HttpResponseMessage confirmResponse = await SendIdempotentAsync(
            supervisorClient, $"/api/v1/supplies/{supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = lineId, receivedQuantity = 20m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, confirmResponse.StatusCode);
    }
}
