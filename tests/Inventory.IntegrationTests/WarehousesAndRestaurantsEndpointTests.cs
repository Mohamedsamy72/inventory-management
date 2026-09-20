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

/// <summary>Tasks 5.8-5.9. Focused on what is genuinely specific to these two resources.
/// Pagination/permission/tenant-isolation coverage is shared infrastructure already proven by
/// <see cref="CategoriesEndpointTests"/>.
///
/// Restaurant creation no longer references any warehouse at all (Change 1 reversed ADR-028 -
/// a restaurant can receive from more than one warehouse, so there is no single default to
/// validate here); the equivalent warehouse-validity/tenant-isolation coverage now lives in
/// <see cref="SupplyRequestTests"/>, at the point a warehouse is actually chosen (request
/// creation).</summary>
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

    /// <summary>Change 1: restaurant creation declares no warehouse field at all - a restaurant
    /// is a pure consumption node, receiving from whichever warehouse a given supply request
    /// names, not pinned to one at creation time.</summary>
    [Fact]
    public async Task Creating_A_Restaurant_Without_Any_Warehouse_Succeeds()
    {
        using HttpClient client = await LoginAsOwnerAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع مدينة نصر", code = "RST-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RestaurantDto>();
        Assert.True(body!.IsActive);
    }

    /// <summary>A restaurant update also declares no warehouse field - only name/address/
    /// description are editable through this endpoint.</summary>
    [Fact]
    public async Task Updating_A_Restaurant_Does_Not_Require_A_Warehouse()
    {
        using HttpClient client = await LoginAsOwnerAsync();
        using HttpResponseMessage createResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع قبل التعديل", code = "RST-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var restaurant = await createResponse.Content.ReadFromJsonAsync<RestaurantDto>();

        using HttpResponseMessage updateResponse = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/restaurants/{restaurant!.Id}",
            new { nameArabic = "فرع بعد التعديل", address = (string?)null, description = (string?)null });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<RestaurantDto>();
        Assert.Equal("فرع بعد التعديل", updated!.NameArabic);
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
        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            ownerClient, "/api/v1/restaurants",
            new { nameArabic = "فرع الاختبار", code = "RST-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var restaurant = await restaurantResponse.Content.ReadFromJsonAsync<RestaurantDto>();

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Guid companyId = (await context.Restaurants.IgnoreQueryFilters().FirstAsync(r => r.Id == restaurant!.Id)).CompanyId;
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
    private sealed record RestaurantDto(Guid Id, string NameArabic, string Code, bool IsActive);
    private sealed record WarehouseNameDto(Guid Id, string NameArabic, string Code);
    private sealed record RestaurantNameDto(Guid Id, string NameArabic, string Code);
}
