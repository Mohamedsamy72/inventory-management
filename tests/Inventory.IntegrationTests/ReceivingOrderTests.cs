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

/// <summary>Phase 8 - Receiving (docs/09 tasks 8.1-8.13). Scenarios 1-2 (docs/23) are the
/// project's own flagged highest-risk case: verification must post only the DELTA, never the
/// full actual quantity (docs/09 §Phase 8 "Risks").</summary>
public sealed class ReceivingOrderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ReceivingOrderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record LineDto(Guid Id, Guid ItemId, decimal ExpectedQuantity, decimal? ActualQuantity, Guid UnitId, decimal? UnitCost, decimal? TotalCost, bool Reconciled);
    private sealed record OrderDto(Guid Id, string DocumentNumber, Guid WarehouseId, Guid? SupplierId, ReceivingOrderStatus Status, DateOnly BusinessDate, Guid? ReversedBy, string? ReversalReason, List<LineDto> Lines);
    private sealed record StockLineDto(Guid ItemId, decimal Balance, decimal InTransit, decimal Available, decimal? AverageUnitCost);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid WarehouseId, Guid ItemId, Guid BaseUnitId);

    /// <summary>Order DTOs carry <see cref="ReceivingOrderStatus"/> - the server serializes
    /// enums as strings (Program.cs's <c>JsonStringEnumConverter</c>), so the client side needs
    /// the same converter to read them back.</summary>
    private static readonly JsonSerializerOptions OrderJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<Seed> SeedAsync()
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
            client, "/api/v1/units", new { nameArabic = "كيلوجرام " + Guid.NewGuid().ToString("N")[..6], abbreviation = (string?)null });
        var unit = await unitResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage itemResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items",
            new { nameArabic = "صنف " + Guid.NewGuid().ToString("N")[..6], categoryId = category!.Id, baseUnitId = unit!.Id, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>();

        return new Seed(company.Id, client, warehouse!.Id, item!.Id, item.BaseUnitId);
    }

    /// <summary>AuthTestHelpers.SendJsonAsync has no idempotency-header hook - replicate its CSRF
    /// dance here with the header attached, since submit/verify/reverse require one.</summary>
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

    private static async Task<OrderDto> CreateSubmittedOrderAsync(Seed seed, decimal expectedQuantity, decimal unitCost)
    {
        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions);

        using HttpResponseMessage lineResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity, unitCost, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, lineResponse.StatusCode);

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        return (await submitResponse.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions))!;
    }

    private async Task<StockBalance> GetBalanceAsync(Guid warehouseId, Guid itemId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
    }

    [Fact]
    public async Task Scenario_1_And_2_Submit_Then_Verify_Posts_Only_The_Delta()
    {
        Seed seed = await SeedAsync();

        // Baseline: an initial 500 KG receipt, matching docs/23 Scenario 1's starting balance.
        await CreateSubmittedOrderAsync(seed, 500m, 10m);
        StockBalance afterBaseline = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(500m, afterBaseline.Quantity);

        // Scenario 1: submit 100 KG @ 12.00 -> balance 600 KG.
        OrderDto order = await CreateSubmittedOrderAsync(seed, 100m, 12.00m);
        StockBalance afterSubmit = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(600m, afterSubmit.Quantity);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        bool postedLedgerRowExists = await context.StockLedgerEntries.IgnoreQueryFilters()
            .AnyAsync(l => l.ItemId == seed.ItemId && l.MovementType == MovementType.IncomingPosted && l.BaseQuantity == 100m);
        Assert.True(postedLedgerRowExists);

        // Scenario 2: verify actual=80 vs expected=100 -> balance 580 KG (delta -20), NEVER
        // 680 (full actual added) or 660 (delta miscomputed against the running balance).
        Guid lineId = order.Lines.Single().Id;
        using HttpResponseMessage verifyResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify",
            new { lines = new[] { new { lineId, actualQuantity = 80m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verified = await verifyResponse.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions);
        Assert.Equal(ReceivingOrderStatus.Verified, verified!.Status);

        StockBalance afterVerify = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(580m, afterVerify.Quantity);
        Assert.NotEqual(680m, afterVerify.Quantity);
        Assert.NotEqual(660m, afterVerify.Quantity);

        bool reconciliationLedgerRowExists = await context.StockLedgerEntries.IgnoreQueryFilters()
            .AnyAsync(l => l.ItemId == seed.ItemId && l.MovementType == MovementType.IncomingReconciliation && l.BaseQuantity == -20m);
        Assert.True(reconciliationLedgerRowExists);

        // Task 8.5/12.2: the reconciliation variance is also logged as a ReceivingVariance
        // Discrepancy, not just posted to the ledger.
        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(d => d.ReferenceId == order.Id && d.ItemId == seed.ItemId);
        Assert.Equal(DiscrepancyType.ReceivingVariance, discrepancy.Type);
        Assert.Equal(-20m, discrepancy.Variance);
        Assert.Equal(DiscrepancyStatus.Open, discrepancy.Status);
    }

    [Fact]
    public async Task Verifying_An_Already_Verified_Order_Is_Rejected()
    {
        Seed seed = await SeedAsync();
        OrderDto order = await CreateSubmittedOrderAsync(seed, 100m, 12.00m);
        Guid lineId = order.Lines.Single().Id;

        var verifyPayload = new { lines = new[] { new { lineId, actualQuantity = 80m } } };
        using HttpResponseMessage first = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify", verifyPayload, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using HttpResponseMessage second = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify", verifyPayload, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        StockBalance balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(80m, balance.Quantity);
    }

    [Fact]
    public async Task Reversing_A_Submitted_Order_Drives_The_Balance_Back_To_Zero()
    {
        Seed seed = await SeedAsync();
        OrderDto order = await CreateSubmittedOrderAsync(seed, 100m, 12.00m);

        using HttpResponseMessage reverseResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/reverse", new { reason = "توالف الشحنة" }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        var reversed = await reverseResponse.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions);
        Assert.Equal(ReceivingOrderStatus.Reversed, reversed!.Status);

        StockBalance balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(0m, balance.Quantity);
    }

    [Fact]
    public async Task Idempotent_Resubmit_With_The_Same_Key_Posts_Stock_Exactly_Once()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 100m, unitCost = 12m, notes = (string?)null });

        string key = Guid.NewGuid().ToString();
        using HttpResponseMessage first = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using HttpResponseMessage second = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        StockBalance balance = await GetBalanceAsync(seed.WarehouseId, seed.ItemId);
        Assert.Equal(100m, balance.Quantity);
    }

    [Fact]
    public async Task Submit_Without_An_Idempotency_Key_Is_Rejected()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 100m, unitCost = 12m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.BadRequest, submitResponse.StatusCode);
    }

    [Fact]
    public async Task Warehouse_Staff_Outside_Their_Scope_Gets_403()
    {
        Seed seed = await SeedAsync();

        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        // Deliberately no scope assignment - the staff user has zero authorized warehouses.
        HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            staffClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Never_Sees_Cost_Fields()
    {
        Seed seed = await SeedAsync();
        OrderDto order = await CreateSubmittedOrderAsync(seed, 100m, 12.00m);

        (User adminUser, string adminPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, adminUser.Id, RoleName.Admin);
        HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, adminUser, adminPassword);

        using HttpResponseMessage response = await adminClient.GetAsync($"/api/v1/receiving-orders/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var asAdmin = await response.Content.ReadFromJsonAsync<OrderDto>(OrderJsonOptions);

        Assert.All(asAdmin!.Lines, line => Assert.Null(line.UnitCost));
        Assert.All(asAdmin.Lines, line => Assert.Null(line.TotalCost));
    }

    [Fact]
    public async Task Owner_Sees_Cost_Fields_And_Warehouse_Stock_Reports_Balance()
    {
        Seed seed = await SeedAsync();
        await CreateSubmittedOrderAsync(seed, 100m, 12.00m);

        using HttpResponseMessage stockResponse = await seed.OwnerClient.GetAsync($"/api/v1/warehouses/{seed.WarehouseId}/stock");
        Assert.Equal(HttpStatusCode.OK, stockResponse.StatusCode);
        var stockLines = await stockResponse.Content.ReadFromJsonAsync<List<StockLineDto>>();

        StockLineDto line = stockLines!.Single(l => l.ItemId == seed.ItemId);
        Assert.Equal(100m, line.Balance);
        Assert.Equal(100m, line.Available);
        Assert.Equal(12.00m, line.AverageUnitCost);
    }
}
