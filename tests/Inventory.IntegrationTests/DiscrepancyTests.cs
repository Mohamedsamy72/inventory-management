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

/// <summary>Phase 12 - Discrepancies & Reconciliation (docs/09 tasks 12.1-12.7, ADR-021).
/// Resolution must never post a stock movement - the risk docs/09 itself flags for this
/// phase.</summary>
public sealed class DiscrepancyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiscrepancyTests(WebApplicationFactory<Program> factory)
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
    private sealed record DiscrepancyDto(Guid Id, string DocumentNumber, DiscrepancyType Type, ReferenceType ReferenceType, Guid ReferenceId, Guid? ReferenceLineId, Guid? WarehouseId, Guid? RestaurantId, Guid? ItemId, decimal ExpectedQuantity, decimal ActualQuantity, decimal Variance, DiscrepancyStatus Status, string? Reason, Guid? ResolvedBy, DateTimeOffset? ResolvedAt, DateTimeOffset CreatedAt);
    private sealed record PageDto<T>(List<T> Items, string? NextCursor);

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

    [Fact]
    public async Task Confirmation_Variance_Is_Listed_As_SupplyReceiptVariance_Scoped_To_Restaurant_Supervisor()
    {
        Seed seed = await SeedAsync(openingBalance: 20m);

        using HttpResponseMessage createRequest = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId });
        var request = await createRequest.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request!.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 20m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        var submitted = await submitResponse.Content.ReadFromJsonAsync<RequestDto>(JsonOptions);
        Guid reqLineId = submitted!.Lines.Single().Id;

        using HttpResponseMessage fulfillResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/fulfill",
            new { lines = new[] { new { supplyRequestItemId = reqLineId, fulfilledQuantity = 20m } } });
        var fulfillResult = await fulfillResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        using HttpResponseMessage dispatchResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supplies/{fulfillResult!.Supply.Id}/dispatch", new { });
        var dispatchResult = await dispatchResponse.Content.ReadFromJsonAsync<SupplyOperationDto>(JsonOptions);

        Guid supplyLineId = dispatchResult!.Supply.Lines.Single().Id;
        using HttpResponseMessage confirmResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/supplies/{dispatchResult.Supply.Id}/confirm",
            new { lines = new[] { new { supplyItemId = supplyLineId, receivedQuantity = 18m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        (User supervisorUser, string supervisorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisorUser.Id, RoleName.RestaurantSupervisor);
        HttpClient supervisorClient = await AuthTestHelpers.LoginAsAsync(_factory, supervisorUser, supervisorPassword);

        using HttpResponseMessage scopeResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Put, $"/api/v1/users/{supervisorUser.Id}/scope",
            new { warehouseIds = Array.Empty<Guid>(), restaurantIds = new[] { seed.RestaurantId } });
        Assert.Equal(HttpStatusCode.OK, scopeResponse.StatusCode);

        using HttpResponseMessage listResponse = await supervisorClient.GetAsync("/api/v1/discrepancies");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<DiscrepancyDto>>(JsonOptions);
        DiscrepancyDto discrepancy = page!.Items.Single(d => d.ReferenceId == dispatchResult.Supply.Id);
        Assert.Equal(DiscrepancyType.SupplyReceiptVariance, discrepancy.Type);
        Assert.Equal(-2m, discrepancy.Variance);

        // The supervisor cannot resolve it (only Owner/Admin hold discrepancies:resolve).
        using HttpResponseMessage resolveAttempt = await AuthTestHelpers.PostJsonAsync(
            supervisorClient, $"/api/v1/discrepancies/{discrepancy.Id}/resolve", new { reason = "محاولة" });
        Assert.Equal(HttpStatusCode.Forbidden, resolveAttempt.StatusCode);
    }

    [Fact]
    public async Task Receiving_Verify_Variance_Is_Listed_And_Resolvable()
    {
        Seed seed = await SeedAsync(openingBalance: 0m);

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 100m, unitCost = 10m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        var submitted = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid lineId = submitted.GetProperty("lines")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage verifyResponse = await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify",
            new { lines = new[] { new { lineId, actualQuantity = 80m } } }, Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        using HttpResponseMessage listResponse = await seed.OwnerClient.GetAsync("/api/v1/discrepancies");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<DiscrepancyDto>>(JsonOptions);
        DiscrepancyDto discrepancy = page!.Items.Single(d => d.ReferenceId == order.Id);
        Assert.Equal(DiscrepancyType.ReceivingVariance, discrepancy.Type);
        Assert.Equal(-20m, discrepancy.Variance);
        Assert.Equal(DiscrepancyStatus.Open, discrepancy.Status);

        using HttpResponseMessage resolveResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/discrepancies/{discrepancy.Id}/resolve", new { reason = "تحقق من الفاتورة - نقص فعلي" });
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<DiscrepancyDto>(JsonOptions);
        Assert.Equal(DiscrepancyStatus.Resolved, resolved!.Status);
    }

    [Fact]
    public async Task Resolving_Writes_Audit_And_No_Stock_Ledger_Row()
    {
        Seed seed = await SeedAsync(openingBalance: 0m);

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 50m, unitCost = 10m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        var submitted = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid lineId = submitted.GetProperty("lines")[0].GetProperty("id").GetGuid();

        await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify",
            new { lines = new[] { new { lineId, actualQuantity = 45m } } }, Guid.NewGuid().ToString());

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking().FirstAsync(d => d.ReferenceId == order.Id);
        int ledgerCountBefore = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == seed.CompanyId);

        using HttpResponseMessage resolveResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/discrepancies/{discrepancy.Id}/resolve", new { reason = "توالف أثناء النقل" });
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        int ledgerCountAfter = await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == seed.CompanyId);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);

        bool auditRowExists = await context.AuditLogs.IgnoreQueryFilters()
            .AnyAsync(a => a.Action == "DISCREPANCY_RESOLVED" && a.EntityId == discrepancy.Id);
        Assert.True(auditRowExists);
    }

    [Fact]
    public async Task Resolving_Without_A_Reason_Is_Rejected()
    {
        Seed seed = await SeedAsync(openingBalance: 0m);

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 50m, unitCost = 10m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        var submitted = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid lineId = submitted.GetProperty("lines")[0].GetProperty("id").GetGuid();

        await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify",
            new { lines = new[] { new { lineId, actualQuantity = 45m } } }, Guid.NewGuid().ToString());

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking().FirstAsync(d => d.ReferenceId == order.Id);

        using HttpResponseMessage resolveResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/discrepancies/{discrepancy.Id}/resolve", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resolveResponse.StatusCode);
    }

    [Fact]
    public async Task Warehouse_Staff_Sees_Only_Their_Warehouses_Discrepancies()
    {
        Seed seed = await SeedAsync(openingBalance: 0m);

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 50m, unitCost = 10m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        var submitted = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid lineId = submitted.GetProperty("lines")[0].GetProperty("id").GetGuid();

        await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify",
            new { lines = new[] { new { lineId, actualQuantity = 45m } } }, Guid.NewGuid().ToString());

        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);
        // Deliberately no warehouse scope assigned - sees nothing.

        using HttpResponseMessage listResponse = await staffClient.GetAsync("/api/v1/discrepancies");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = await listResponse.Content.ReadFromJsonAsync<PageDto<DiscrepancyDto>>(JsonOptions);
        Assert.DoesNotContain(page!.Items, d => d.ReferenceId == order.Id);

        using HttpResponseMessage scopeResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Put, $"/api/v1/users/{staffUser.Id}/scope",
            new { warehouseIds = new[] { seed.WarehouseId }, restaurantIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.OK, scopeResponse.StatusCode);

        using HttpResponseMessage secondListResponse = await staffClient.GetAsync("/api/v1/discrepancies");
        var secondPage = await secondListResponse.Content.ReadFromJsonAsync<PageDto<DiscrepancyDto>>(JsonOptions);
        Assert.Contains(secondPage!.Items, d => d.ReferenceId == order.Id);
    }

    [Fact]
    public async Task Warehouse_Staff_Cannot_Resolve_Even_In_Scope()
    {
        Seed seed = await SeedAsync(openingBalance: 0m);

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/receiving-orders",
            new { warehouseId = seed.WarehouseId, supplierId = (Guid?)null, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        var order = await createResponse.Content.ReadFromJsonAsync<IdDto>();

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order!.Id}/lines",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, expectedQuantity = 50m, unitCost = 10m, notes = (string?)null });

        using HttpResponseMessage submitResponse = await SendIdempotentAsync(seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/submit", new { }, Guid.NewGuid().ToString());
        var submitted = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Guid lineId = submitted.GetProperty("lines")[0].GetProperty("id").GetGuid();

        await SendIdempotentAsync(
            seed.OwnerClient, $"/api/v1/receiving-orders/{order.Id}/verify",
            new { lines = new[] { new { lineId, actualQuantity = 45m } } }, Guid.NewGuid().ToString());

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Discrepancy discrepancy = await context.Discrepancies.IgnoreQueryFilters().AsNoTracking().FirstAsync(d => d.ReferenceId == order.Id);

        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);

        using HttpResponseMessage resolveResponse = await AuthTestHelpers.PostJsonAsync(
            staffClient, $"/api/v1/discrepancies/{discrepancy.Id}/resolve", new { reason = "محاولة غير مصرح بها" });
        Assert.Equal(HttpStatusCode.Forbidden, resolveResponse.StatusCode);
    }
}
