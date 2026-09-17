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

/// <summary>Tasks 5.8-5.9. Focused on what is genuinely specific to these two resources: the
/// ADR-028 SW-6 serving-warehouse validation at restaurant creation. Pagination/permission/
/// tenant-isolation coverage is shared infrastructure already proven by
/// <see cref="CategoriesEndpointTests"/>.</summary>
public sealed class WarehousesAndRestaurantsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WarehousesAndRestaurantsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> LoginAsOwnerAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        return await AuthTestHelpers.LoginAsAsync(_factory, owner, password);
    }

    [Fact]
    public async Task Creating_A_Warehouse_Succeeds()
    {
        using HttpClient client = await LoginAsOwnerAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses",
            new { nameArabic = "المستودع الرئيسي", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Creating_A_Warehouse_With_A_Duplicate_Code_Returns_409()
    {
        using HttpClient client = await LoginAsOwnerAsync();
        string code = "WH-" + Guid.NewGuid().ToString("N")[..6];

        await AuthTestHelpers.PostJsonAsync(client, "/api/v1/warehouses", new { nameArabic = "مستودع 1", code, address = (string?)null, description = (string?)null });
        using HttpResponseMessage second = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses", new { nameArabic = "مستودع 2", code, address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Creating_A_Restaurant_With_An_Active_Serving_Warehouse_Succeeds()
    {
        using HttpClient client = await LoginAsOwnerAsync();
        Guid warehouseId = await CreateWarehouseAsync(client);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع مدينة نصر", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = warehouseId, address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestaurantDto>();
        Assert.Equal(warehouseId, body!.DefaultServingWarehouseId);
    }

    /// <summary>ADR-028 SW-6: a restaurant cannot be created pointing at a warehouse that does
    /// not exist in this tenant - including a real warehouse id from a DIFFERENT company, which
    /// the tenant query filter makes indistinguishable from nonexistent.</summary>
    [Fact]
    public async Task Creating_A_Restaurant_With_A_Nonexistent_Serving_Warehouse_Returns_409()
    {
        using HttpClient client = await LoginAsOwnerAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع بلا مستودع", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = Guid.NewGuid(), address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SERVING_WAREHOUSE_UNAVAILABLE", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Creating_A_Restaurant_With_An_Inactive_Serving_Warehouse_Returns_409()
    {
        using HttpClient client = await LoginAsOwnerAsync();
        Guid warehouseId = await CreateWarehouseAsync(client);
        using HttpResponseMessage deactivate = await AuthTestHelpers.PostJsonAsync(client, $"/api/v1/warehouses/{warehouseId}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع بمستودع معطل", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = warehouseId, address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("SERVING_WAREHOUSE_UNAVAILABLE", body, StringComparison.Ordinal);
    }

    /// <summary>A real warehouse id belonging to a DIFFERENT company must be rejected exactly
    /// like a nonexistent one, never silently accepted (which would violate ADR-016's tenant
    /// isolation guarantee).</summary>
    [Fact]
    public async Task Creating_A_Restaurant_With_Another_Companys_Warehouse_Returns_409()
    {
        using HttpClient ownerAClient = await LoginAsOwnerAsync();
        using HttpClient ownerBClient = await LoginAsOwnerAsync();
        Guid otherCompanyWarehouseId = await CreateWarehouseAsync(ownerBClient);

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            ownerAClient, "/api/v1/restaurants",
            new { nameArabic = "فرع بمستودع شركة أخرى", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = otherCompanyWarehouseId, address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>A Warehouse Staff / Restaurant Supervisor user holds no `warehouses:manage` or
    /// `restaurants:manage` permission at all (docs/03 §"Warehouses & Branches" - both ❌ for
    /// both roles), yet every screen they use (receiving, supply requests, supplies) references
    /// a warehouseId/restaurantId they need to display as a name - proves the lower-privileged
    /// "/names" projection is reachable where the full manage-gated list is correctly not.</summary>
    [Fact]
    public async Task Warehouse_Staff_Can_List_Warehouse_Names_But_Not_The_Full_Manage_Gated_List()
    {
        using HttpClient ownerClient = await LoginAsOwnerAsync();
        Guid warehouseId = await CreateWarehouseAsync(ownerClient);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid companyId = (await context.Warehouses.IgnoreQueryFilters().FirstAsync(w => w.Id == warehouseId)).CompanyId;
        (User staffUser, string staffPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, companyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, staffUser.Id, RoleName.WarehouseStaff);
        using HttpClient staffClient = await AuthTestHelpers.LoginAsAsync(_factory, staffUser, staffPassword);

        using HttpResponseMessage namesResponse = await staffClient.GetAsync("/api/v1/warehouses/names");
        Assert.Equal(HttpStatusCode.OK, namesResponse.StatusCode);
        var names = await namesResponse.Content.ReadFromJsonAsync<List<WarehouseNameDto>>();
        Assert.Contains(names!, w => w.Id == warehouseId);

        using HttpResponseMessage fullListResponse = await staffClient.GetAsync("/api/v1/warehouses");
        Assert.Equal(HttpStatusCode.Forbidden, fullListResponse.StatusCode);
    }

    [Fact]
    public async Task Restaurant_Supervisor_Can_List_Restaurant_Names_But_Not_The_Full_Manage_Gated_List()
    {
        using HttpClient ownerClient = await LoginAsOwnerAsync();
        Guid warehouseId = await CreateWarehouseAsync(ownerClient);
        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            ownerClient, "/api/v1/restaurants",
            new { nameArabic = "فرع الاختبار", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = warehouseId, address = (string?)null, description = (string?)null });
        var restaurant = await restaurantResponse.Content.ReadFromJsonAsync<RestaurantDto>();

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid companyId = (await context.Warehouses.IgnoreQueryFilters().FirstAsync(w => w.Id == warehouseId)).CompanyId;
        (User supervisorUser, string supervisorPassword) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, companyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisorUser.Id, RoleName.RestaurantSupervisor);
        using HttpClient supervisorClient = await AuthTestHelpers.LoginAsAsync(_factory, supervisorUser, supervisorPassword);

        using HttpResponseMessage namesResponse = await supervisorClient.GetAsync("/api/v1/restaurants/names");
        Assert.Equal(HttpStatusCode.OK, namesResponse.StatusCode);
        var names = await namesResponse.Content.ReadFromJsonAsync<List<RestaurantNameDto>>();
        Assert.Contains(names!, r => r.Id == restaurant!.Id);

        using HttpResponseMessage fullListResponse = await supervisorClient.GetAsync("/api/v1/restaurants");
        Assert.Equal(HttpStatusCode.Forbidden, fullListResponse.StatusCode);
    }

    private static async Task<Guid> CreateWarehouseAsync(HttpClient client)
    {
        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses",
            new { nameArabic = "مستودع اختبار", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var warehouse = await response.Content.ReadFromJsonAsync<WarehouseDto>();
        return warehouse!.Id;
    }

    private sealed record WarehouseDto(Guid Id, string NameArabic, string Code, bool IsActive);
    private sealed record RestaurantDto(Guid Id, string NameArabic, string Code, Guid DefaultServingWarehouseId, bool IsActive);
    private sealed record WarehouseNameDto(Guid Id, string NameArabic, string Code);
    private sealed record RestaurantNameDto(Guid Id, string NameArabic, string Code);
}
