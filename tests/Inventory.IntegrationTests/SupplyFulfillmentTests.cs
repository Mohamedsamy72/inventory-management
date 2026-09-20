using System.Net;
using System.Net.Http.Json;
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

/// <summary>Phase 10 - Fulfilment & Dispatch (docs/09 tasks 10.1-10.8, ADR-017/ADR-019). Neither
/// fulfilment nor dispatch may touch the stock ledger or balance - docs/23 Scenario 3's
/// acceptance criterion is that stock stays byte-identical through this entire phase.</summary>
public sealed class SupplyFulfillmentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SupplyFulfillmentTests(WebApplicationFactory<Program> factory)
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

    private async Task<Seed> SeedAsync(decimal openingBalance = 0m, decimal openingCost = 10m)
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

        using HttpResponseMessage tokenResponse = await seed.OwnerClient.GetAsync("/api/v1/auth/csrf-token");
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/receiving-orders/{order.Id}/submit")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenBody!["csrfToken"]);
        request.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
        using HttpResponseMessage submitResponse = await seed.OwnerClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
    }

    private static async Task<RequestDto> CreateSubmittedRequestAsync(Seed seed, decimal requestedQuantity)
    {
        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseId });
        var request = await createResponse.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request!.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity, notes = (string?)null });

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        return (await submitResponse.Content.ReadFromJsonAsync<RequestDto>(JsonOptions))!;
    }

    private async Task<(decimal Quantity, int LedgerCount)> GetStockSnapshotAsync(Guid companyId, Guid warehouseId, Guid itemId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        StockBalance? balance = await context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
        int ledgerCount = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == companyId);
        return (balance?.Quantity ?? 0m, ledgerCount);
    }

    [Fact]
    public async Task Full_Fulfilment_And_Dispatch_Leaves_Stock_Exactly_Unchanged()
    {
        Seed seed = await SeedAsync(openingBalance: 580m);
        (decimal quantityBefore, int ledgerCountBefore) = await GetStockSnapshotAsync(seed.CompanyId, seed.WarehouseId, seed.ItemId);

        RequestDto request = await CreateSubmittedRequestAsync(seed, 100m);
        Guid lineId = request.Lines.Single().Id;

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 100m } } });
        Assert.Equal(HttpStatusCode.Created, fulfillResponse.StatusCode);
        var fulfillResult = await fulfillResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);
        Assert.Equal(SupplyStatus.Prepared, fulfillResult!.Supply.Status);
        Assert.Empty(fulfillResult.InsufficientStockItemIds);

        using HttpResponseMessage requestAfterFulfill = await seed.OwnerClient.GetAsync($"/api/v1/supply-requests/{request.Id}");
        var refetchedRequest = await requestAfterFulfill.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);
        Assert.Equal(SupplyRequestStatus.Fulfilled, refetchedRequest!.Status);

        using HttpResponseMessage dispatchResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{fulfillResult.Supply.Id}/dispatch", new { });
        Assert.Equal(HttpStatusCode.OK, dispatchResponse.StatusCode);
        var dispatchResult = await dispatchResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);
        Assert.Equal(SupplyStatus.Dispatched, dispatchResult!.Supply.Status);

        (decimal quantityAfter, int ledgerCountAfter) = await GetStockSnapshotAsync(seed.CompanyId, seed.WarehouseId, seed.ItemId);
        Assert.Equal(quantityBefore, quantityAfter);
        Assert.Equal(580m, quantityAfter);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);
    }

    [Fact]
    public async Task Partial_Fulfilment_Leaves_Request_PartiallyFulfilled_And_Line_Fulfillable_Again()
    {
        Seed seed = await SeedAsync(openingBalance: 580m);
        RequestDto request = await CreateSubmittedRequestAsync(seed, 100m);
        Guid lineId = request.Lines.Single().Id;

        using HttpResponseMessage firstFulfill = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 60m } } });
        Assert.Equal(HttpStatusCode.Created, firstFulfill.StatusCode);

        using HttpResponseMessage afterFirst = await seed.OwnerClient.GetAsync($"/api/v1/supply-requests/{request.Id}");
        var afterFirstDto = await afterFirst.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);
        Assert.Equal(SupplyRequestStatus.PartiallyFulfilled, afterFirstDto!.Status);
        Assert.Equal(60m, afterFirstDto.Lines.Single().FulfilledQuantity);

        using HttpResponseMessage secondFulfill = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 40m } } });
        Assert.Equal(HttpStatusCode.Created, secondFulfill.StatusCode);

        using HttpResponseMessage afterSecond = await seed.OwnerClient.GetAsync($"/api/v1/supply-requests/{request.Id}");
        var afterSecondDto = await afterSecond.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);
        Assert.Equal(SupplyRequestStatus.Fulfilled, afterSecondDto!.Status);
        Assert.Equal(100m, afterSecondDto.Lines.Single().FulfilledQuantity);
    }

    [Fact]
    public async Task Zero_Fulfilled_Line_Is_Permitted_And_Advisory_Raised_Without_Blocking()
    {
        // Nothing in stock at all (openingBalance defaults to 0), so any positive fulfilment is
        // advisory-insufficient but still succeeds (ADR-019 - warns, never blocks).
        Seed seed = await SeedAsync();
        RequestDto request = await CreateSubmittedRequestAsync(seed, 100m);
        Guid lineId = request.Lines.Single().Id;

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 30m } } });
        Assert.Equal(HttpStatusCode.Created, fulfillResponse.StatusCode);
        var result = await fulfillResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        Assert.Contains(seed.ItemId, result!.InsufficientStockItemIds);
        Assert.Equal(SupplyStatus.Prepared, result.Supply.Status);

        (decimal quantityAfter, _) = await GetStockSnapshotAsync(seed.CompanyId, seed.WarehouseId, seed.ItemId);
        Assert.Equal(0m, quantityAfter);
    }

    [Fact]
    public async Task Cancel_Is_Refused_After_Dispatch()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        RequestDto request = await CreateSubmittedRequestAsync(seed, 20m);
        Guid lineId = request.Lines.Single().Id;

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 20m } } });
        var fulfillResult = await fulfillResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        using HttpResponseMessage dispatchResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{fulfillResult!.Supply.Id}/dispatch", new { });
        Assert.Equal(HttpStatusCode.OK, dispatchResponse.StatusCode);

        using HttpResponseMessage cancelResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{fulfillResult.Supply.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task Cancel_From_Prepared_Succeeds()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        RequestDto request = await CreateSubmittedRequestAsync(seed, 20m);
        Guid lineId = request.Lines.Single().Id;

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 20m } } });
        var fulfillResult = await fulfillResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        using HttpResponseMessage cancelResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{fulfillResult!.Supply.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<SupplyDto>(JsonOptions);
        Assert.Equal(SupplyStatus.Cancelled, cancelled!.Status);
    }

    [Fact]
    public async Task Warehouse_Staff_Outside_Scope_Cannot_Fulfill_Or_Dispatch()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        RequestDto request = await CreateSubmittedRequestAsync(seed, 20m);
        Guid lineId = request.Lines.Single().Id;

        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);
        // Deliberately no scope assignment.

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            staffClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = lineId, fulfilledQuantity = 20m } } });
        Assert.Equal(HttpStatusCode.Forbidden, fulfillResponse.StatusCode);
    }
}
