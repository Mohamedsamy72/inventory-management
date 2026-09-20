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

/// <summary>Phase 13 - Physical Stock Counts & Adjustments (docs/09 tasks 13.1-13.11,
/// ADR-018/020/021). Blind counting implemented only in the UI would leak the system quantity
/// over the wire and defeat the entire control - task 13.4's own flagged risk.</summary>
public sealed class StockCountTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StockCountTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record CountLineDto(Guid Id, Guid ItemId, decimal? SystemQuantity, decimal? PhysicalQuantity, decimal? Variance, Guid BaseUnitId, string? Notes);
    private sealed record CountDto(Guid Id, string DocumentNumber, Guid WarehouseId, StockCountStatus Status, bool IsBlindCount, Guid OpenedBy, DateTimeOffset OpenedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, List<CountLineDto> Lines);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid WarehouseId, Guid ItemId, Guid BaseUnitId);

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

        var seed = new Seed(company.Id, client, warehouse!.Id, item!.Id, item.BaseUnitId);

        if (openingBalance > 0)
        {
            await PostReceivingAsync(seed, openingBalance, 10m);
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
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        return await client.SendAsync(request);
    }

    private static async Task<CountDto> CreateInProgressCountAsync(Seed seed, bool isBlindCount)
    {
        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/stock-counts", new { warehouseId = seed.WarehouseId, isBlindCount });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CountDto>(JsonOptions))!;
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
    public async Task Non_Zero_Variance_Posts_An_Adjustment_Valued_At_Current_Wac()
    {
        Seed seed = await SeedAsync(openingBalance: 100m);
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: false);
        Guid lineId = count.Lines.Single(l => l.ItemId == seed.ItemId).Id;
        Assert.Equal(100m, count.Lines.Single().SystemQuantity);

        using HttpResponseMessage recordResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId, physicalQuantity = 95m } } });
        Assert.Equal(HttpStatusCode.OK, recordResponse.StatusCode);
        var recorded = await recordResponse.Content.ReadFromJsonAsync<CountDto>(JsonOptions);
        Assert.Equal(-5m, recorded!.Lines.Single().Variance);

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        using HttpResponseMessage approveResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/approve", new { }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<CountDto>(JsonOptions);
        Assert.Equal(StockCountStatus.Approved, approved!.Status);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(95m, balance);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        StockLedgerEntry ledgerRow = await context.StockLedgerEntries.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(l => l.ItemId == seed.ItemId && l.MovementType == MovementType.PhysicalAdjustment);
        Assert.Equal(-5m, ledgerRow.BaseQuantity);
        Assert.Equal(10m, ledgerRow.UnitCost);

        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking().FirstAsync(d => d.ReferenceId == count.Id);
        Assert.Equal(DiscrepancyType.StockCountVariance, discrepancy.Type);
        Assert.Equal(-5m, discrepancy.Variance);
    }

    [Fact]
    public async Task Zero_Variance_Posts_No_Adjustment()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: false);
        Guid lineId = count.Lines.Single().Id;

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId, physicalQuantity = 50m } } });
        await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/submit", new { });

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        int ledgerCountBefore = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == seed.CompanyId);

        using HttpResponseMessage approveResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/approve", new { }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        int ledgerCountAfter = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == seed.CompanyId);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(50m, balance);
    }

    [Fact]
    public async Task Rejecting_Posts_No_Stock_Effect_And_Returns_To_InProgress()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: false);
        Guid lineId = count.Lines.Single().Id;

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId, physicalQuantity = 40m } } });
        await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/submit", new { });

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        int ledgerCountBefore = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == seed.CompanyId);

        using HttpResponseMessage rejectResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/reject", new { });
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<CountDto>(JsonOptions);
        Assert.Equal(StockCountStatus.InProgress, rejected!.Status);

        int ledgerCountAfter = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == seed.CompanyId);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);

        decimal balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(50m, balance);
    }

    [Fact]
    public async Task Blind_Count_Never_Transmits_The_System_Quantity_Over_The_Wire()
    {
        Seed seed = await SeedAsync(openingBalance: 75m);
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: true);

        CountLineDto createLine = count.Lines.Single();
        Assert.Null(createLine.SystemQuantity);
        Assert.Null(createLine.Variance);

        using HttpResponseMessage getResponse = await seed.OwnerClient.GetAsync($"/api/v1/stock-counts/{count.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<CountDto>(JsonOptions);
        Assert.Null(fetched!.Lines.Single().SystemQuantity);

        using HttpResponseMessage recordResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId = createLine.Id, physicalQuantity = 70m } } });
        var recorded = await recordResponse.Content.ReadFromJsonAsync<CountDto>(JsonOptions);
        // Not just hidden in a formatted view - the raw response must never carry it, even back
        // to the counter who just submitted the figure that produced this exact variance.
        Assert.Null(recorded!.Lines.Single().SystemQuantity);
        Assert.Null(recorded.Lines.Single().Variance);
        Assert.Equal(70m, recorded.Lines.Single().PhysicalQuantity);

        await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/submit", new { });

        // Once approved, the figures become visible for audit review.
        using HttpResponseMessage approveResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/approve", new { }, Guid.NewGuid().ToString());
        var approved = await approveResponse.Content.ReadFromJsonAsync<CountDto>(JsonOptions);
        Assert.Equal(75m, approved!.Lines.Single().SystemQuantity);
        Assert.Equal(-5m, approved.Lines.Single().Variance);
    }

    [Fact]
    public async Task In_Transit_Goods_Are_Excluded_From_The_Expected_Figure()
    {
        Seed seed = await SeedAsync(openingBalance: 100m);

        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/restaurants", new { nameArabic = "فرع", code = "BR-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var restaurant = await restaurantResponse.Content.ReadFromJsonAsync<IdDto>();
        await AuthTestHelpers.AllowWarehouseAsync(seed.OwnerClient, restaurant!.Id, seed.WarehouseId);

        using HttpResponseMessage createRequest = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = restaurant!.Id, warehouseId = seed.WarehouseId });
        var request = await createRequest.Content.ReadFromJsonAsync<JsonElement>();
        Guid requestId = request.GetProperty("id").GetGuid();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{requestId}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 20m, notes = (string?)null });

        using HttpResponseMessage submitReqResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{requestId}/submit", new { });
        var submittedReq = await submitReqResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid reqLineId = submittedReq.GetProperty("lines")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{requestId}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = reqLineId, fulfilledQuantity = 20m } } });
        var fulfillResult = await fulfillResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid supplyId = fulfillResult.GetProperty("supply").GetProperty("id").GetGuid();

        using HttpResponseMessage dispatchResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{supplyId}/dispatch", new { });
        Assert.Equal(HttpStatusCode.OK, dispatchResponse.StatusCode);

        // Balance is still 100 (dispatch never deducts), but 20 is now in transit - the count's
        // expected figure must read 80, never 100 (which would produce a spurious -20 adjustment
        // once the goods are simply confirmed received later, per docs/09's own risk note).
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: false);
        Assert.Equal(80m, count.Lines.Single(l => l.ItemId == seed.ItemId).SystemQuantity);
    }

    [Fact]
    public async Task Warehouse_Staff_Cannot_Approve()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: false);
        Guid lineId = count.Lines.Single().Id;

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId, physicalQuantity = 45m } } });
        await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/submit", new { });

        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);

        using HttpResponseMessage approveResponse = await SendIdempotentAsync(staffClient, $"/api/v1/stock-counts/{count.Id}/approve", new { }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
    }

    [Fact]
    public async Task Lines_Are_Immutable_After_Approval()
    {
        Seed seed = await SeedAsync(openingBalance: 50m);
        CountDto count = await CreateInProgressCountAsync(seed, isBlindCount: false);
        Guid lineId = count.Lines.Single().Id;

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId, physicalQuantity = 50m } } });
        await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/submit", new { });
        await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/approve", new { }, Guid.NewGuid().ToString());

        using HttpResponseMessage lateRecordResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/stock-counts/{count.Id}/record", new { lines = new[] { new { lineId, physicalQuantity = 999m } } });
        Assert.Equal(HttpStatusCode.BadRequest, lateRecordResponse.StatusCode);
    }
}
