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

/// <summary>Phase 9 - Multi-Item Supply Requests (docs/09 tasks 9.1-9.14, ADR-028). Every
/// scenario here must leave the stock ledger completely untouched - creating or submitting a
/// request has ZERO stock effect.</summary>
public sealed class SupplyRequestTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SupplyRequestTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record LineDto(Guid Id, Guid ItemId, decimal RequestedQuantity, decimal FulfilledQuantity, Guid UnitId, string? Notes);
    private sealed record RequestDto(Guid Id, string DocumentNumber, Guid RestaurantId, Guid WarehouseId, SupplyRequestStatus Status, Guid RequestedBy, DateTimeOffset RequestedAt, List<LineDto> Lines);

    private sealed record Seed(Guid CompanyId, HttpClient OwnerClient, Guid RestaurantId, Guid WarehouseId, Guid ItemId, Guid BaseUnitId);

    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
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

        return new Seed(company.Id, client, restaurant!.Id, warehouse.Id, item!.Id, item.BaseUnitId);
    }

    private static async Task<RequestDto> CreateDraftAsync(Seed seed)
    {
        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RequestDto>(RequestJsonOptions))!;
    }

    /// <summary>Scoped to this test's own company, not a raw global count - other test classes
    /// run in parallel against the same shared Postgres instance and post their own ledger rows
    /// concurrently, which would make an unscoped count flaky.</summary>
    private async Task<int> StockLedgerRowCountAsync(Guid companyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(l => l.CompanyId == companyId);
    }

    [Fact]
    public async Task Multi_Line_Draft_Creates_And_Submits_With_Zero_Stock_Effect()
    {
        Seed seed = await SeedAsync();
        int ledgerCountBefore = await StockLedgerRowCountAsync(seed.CompanyId);

        RequestDto request = await CreateDraftAsync(seed);
        Assert.Equal(SupplyRequestStatus.Draft, request.Status);
        Assert.Equal(seed.WarehouseId, request.WarehouseId);

        using HttpResponseMessage lineResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 10m, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, lineResponse.StatusCode);

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = await submitResponse.Content.ReadFromJsonAsync<RequestDto>(RequestJsonOptions);
        Assert.Equal(SupplyRequestStatus.Submitted, submitted!.Status);

        int ledgerCountAfter = await StockLedgerRowCountAsync(seed.CompanyId);
        Assert.Equal(ledgerCountBefore, ledgerCountAfter);
    }

    [Fact]
    public async Task Adding_The_Same_Item_Twice_Merges_Into_One_Line()
    {
        Seed seed = await SeedAsync();
        RequestDto request = await CreateDraftAsync(seed);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 10m, notes = (string?)null });
        using HttpResponseMessage secondAdd = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 5m, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, secondAdd.StatusCode);

        using HttpResponseMessage getResponse = await seed.OwnerClient.GetAsync($"/api/v1/supply-requests/{request.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<RequestDto>(RequestJsonOptions);

        LineDto line = Assert.Single(fetched!.Lines);
        Assert.Equal(15m, line.RequestedQuantity);
    }

    [Fact]
    public async Task Editing_A_Line_Outside_Draft_Is_Rejected()
    {
        Seed seed = await SeedAsync();
        RequestDto request = await CreateDraftAsync(seed);

        using HttpResponseMessage lineResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 10m, notes = (string?)null });
        var line = await lineResponse.Content.ReadFromJsonAsync<LineDto>();

        await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });

        using HttpResponseMessage editResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Put, $"/api/v1/supply-requests/{request.Id}/items/{line!.Id}",
            new { unitId = seed.BaseUnitId, requestedQuantity = 99m, notes = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, editResponse.StatusCode);
    }

    [Fact]
    public async Task Submitting_An_Empty_Request_Is_Rejected()
    {
        Seed seed = await SeedAsync();
        RequestDto request = await CreateDraftAsync(seed);

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.BadRequest, submitResponse.StatusCode);
    }

    [Fact]
    public async Task Submitting_With_An_Inactive_Item_Is_Rejected()
    {
        Seed seed = await SeedAsync();
        RequestDto request = await CreateDraftAsync(seed);

        await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/items",
            new { itemId = seed.ItemId, unitId = seed.BaseUnitId, requestedQuantity = 10m, notes = (string?)null });

        using HttpResponseMessage deactivateResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/items/{seed.ItemId}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.BadRequest, submitResponse.StatusCode);
    }

    [Fact]
    public async Task Restaurant_With_No_Active_Serving_Warehouse_Returns_409_And_Creates_Nothing()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage deactivateResponse = await AuthTestHelpers.PostJsonAsync(seed.OwnerClient, $"/api/v1/warehouses/{seed.WarehouseId}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId });
        Assert.Equal(HttpStatusCode.Conflict, createResponse.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        bool anyCreated = await context.SupplyRequests.IgnoreQueryFilters().AnyAsync(r => r.RestaurantId == seed.RestaurantId);
        Assert.False(anyCreated);
    }

    [Fact]
    public async Task Changing_The_Restaurant_Default_Warehouse_Does_Not_Redirect_Existing_Requests()
    {
        Seed seed = await SeedAsync();
        RequestDto request = await CreateDraftAsync(seed);
        Assert.Equal(seed.WarehouseId, request.WarehouseId);

        using HttpResponseMessage secondWarehouseResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/warehouses", new { nameArabic = "مستودع ثاني", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var secondWarehouse = await secondWarehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage updateRestaurantResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Put, $"/api/v1/restaurants/{seed.RestaurantId}/serving-warehouse",
            new { warehouseId = secondWarehouse!.Id });
        Assert.Equal(HttpStatusCode.OK, updateRestaurantResponse.StatusCode);

        using HttpResponseMessage getResponse = await seed.OwnerClient.GetAsync($"/api/v1/supply-requests/{request.Id}");
        var refetched = await getResponse.Content.ReadFromJsonAsync<RequestDto>(RequestJsonOptions);
        Assert.Equal(seed.WarehouseId, refetched!.WarehouseId);
    }

    [Fact]
    public async Task Restaurant_Supervisor_Cannot_Create_For_Another_Restaurant()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage otherRestaurantResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/restaurants", new { nameArabic = "فرع آخر", code = "BR-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = seed.WarehouseId, address = (string?)null, description = (string?)null });
        var otherRestaurant = await otherRestaurantResponse.Content.ReadFromJsonAsync<IdDto>();

        (User supervisorUser, string supervisorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisorUser.Id, RoleName.RestaurantSupervisor);
        HttpClient supervisorClient = await AuthTestHelpers.LoginAsAsync(_factory, supervisorUser, supervisorPassword);

        using HttpResponseMessage scopeResponse = await AuthTestHelpers.SendJsonAsync(
            seed.OwnerClient, HttpMethod.Put, $"/api/v1/users/{supervisorUser.Id}/scope",
            new { warehouseIds = Array.Empty<Guid>(), restaurantIds = new[] { seed.RestaurantId } });
        Assert.Equal(HttpStatusCode.OK, scopeResponse.StatusCode);

        using HttpResponseMessage forbiddenResponse = await AuthTestHelpers.PostJsonAsync(
            supervisorClient, "/api/v1/supply-requests", new { restaurantId = otherRestaurant!.Id });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);

        using HttpResponseMessage allowedResponse = await AuthTestHelpers.PostJsonAsync(
            supervisorClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId });
        Assert.Equal(HttpStatusCode.Created, allowedResponse.StatusCode);
    }

    /// <summary>ADR-028/task 9.12: <see cref="Inventory.Api.Features.SupplyRequests.CreateSupplyRequestRequest"/>
    /// has no <c>WarehouseId</c> property at all, so a client-posted one is structurally impossible
    /// to bind - proven here by asserting on the PERSISTED row, not merely the response.</summary>
    [Fact]
    public async Task An_Over_Posted_WarehouseId_Has_No_Effect_On_The_Persisted_Row()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage anotherWarehouseResponse = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/warehouses", new { nameArabic = "مستودع مخادع", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var anotherWarehouse = await anotherWarehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            seed.OwnerClient, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = anotherWarehouse!.Id });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RequestDto>(RequestJsonOptions);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        SupplyRequest persisted = await context.SupplyRequests.IgnoreQueryFilters().AsNoTracking().FirstAsync(r => r.Id == created!.Id);

        Assert.Equal(seed.WarehouseId, persisted.WarehouseId);
        Assert.NotEqual(anotherWarehouse.Id, persisted.WarehouseId);
    }

    [Fact]
    public async Task Cross_Restaurant_View_Is_Forbidden_For_Restaurant_Supervisor()
    {
        Seed seed = await SeedAsync();
        RequestDto request = await CreateDraftAsync(seed);

        (User supervisorUser, string supervisorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisorUser.Id, RoleName.RestaurantSupervisor);
        HttpClient supervisorClient = await AuthTestHelpers.LoginAsAsync(_factory, supervisorUser, supervisorPassword);
        // Deliberately no scope assignment - zero authorized restaurants.

        using HttpResponseMessage submitResponse = await AuthTestHelpers.PostJsonAsync(supervisorClient, $"/api/v1/supply-requests/{request.Id}/submit", new { });
        Assert.Equal(HttpStatusCode.Forbidden, submitResponse.StatusCode);
    }
}
