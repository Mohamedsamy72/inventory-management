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

/// <summary>Product decision: a restaurant may only request from warehouses explicitly allowed for it
/// (`restaurant_warehouses`, managed by Owner/Admin via `restaurants:manage`), on top of the
/// existing Active + same-company checks. The relationship is real data, never hard-coded.</summary>
public sealed class RestaurantWarehouseTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RestaurantWarehouseTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record AllowedDto(Guid Id, string NameArabic, string Code);
    private sealed record Seed(Guid CompanyId, HttpClient Owner, Guid RestaurantId, Guid WarehouseA, Guid WarehouseB);

    private async Task<Seed> SeedAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        async Task<Guid> NewWarehouseAsync()
        {
            using HttpResponseMessage r = await AuthTestHelpers.PostJsonAsync(
                client, "/api/v1/warehouses", new { nameArabic = "مستودع " + Guid.NewGuid().ToString("N")[..4], code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
            return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
        }

        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants", new { nameArabic = "فرع", code = "BR-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        Guid restaurantId = (await restaurantResponse.Content.ReadFromJsonAsync<IdDto>())!.Id;

        return new Seed(company.Id, client, restaurantId, await NewWarehouseAsync(), await NewWarehouseAsync());
    }

    private async Task<HttpClient> SupervisorAsync(Seed seed, params Guid[] restaurantIds)
    {
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, RoleName.RestaurantSupervisor);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, password);
        using HttpResponseMessage scope = await AuthTestHelpers.SendJsonAsync(
            seed.Owner, HttpMethod.Put, $"/api/v1/users/{user.Id}/scope", new { warehouseIds = Array.Empty<Guid>(), restaurantIds });
        Assert.Equal(HttpStatusCode.OK, scope.StatusCode);
        return client;
    }

    [Fact]
    public async Task A_Warehouse_That_Is_Not_Allowed_For_The_Restaurant_Is_Rejected_And_Nothing_Is_Created()
    {
        Seed seed = await SeedAsync();
        await AuthTestHelpers.AllowWarehouseAsync(seed.Owner, seed.RestaurantId, seed.WarehouseA);
        using HttpClient supervisor = await SupervisorAsync(seed, seed.RestaurantId);

        using HttpResponseMessage denied = await AuthTestHelpers.PostJsonAsync(
            supervisor, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseB });
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Contains("WAREHOUSE_UNAVAILABLE", await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using HttpResponseMessage allowed = await AuthTestHelpers.PostJsonAsync(
            supervisor, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseA });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(1, await context.SupplyRequests.IgnoreQueryFilters().CountAsync(r => r.RestaurantId == seed.RestaurantId));
    }

    [Fact]
    public async Task A_Restaurant_With_No_Allowed_Warehouses_Cannot_Create_Any_Request()
    {
        Seed seed = await SeedAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            seed.Owner, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseA });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Supervisor_Selector_Lists_Only_The_Allowed_Active_Warehouses_Of_Their_Own_Restaurant()
    {
        Seed seed = await SeedAsync();
        await AuthTestHelpers.AllowWarehouseAsync(seed.Owner, seed.RestaurantId, seed.WarehouseA, seed.WarehouseB);
        using HttpClient supervisor = await SupervisorAsync(seed, seed.RestaurantId);

        var both = await supervisor.GetFromJsonAsync<List<AllowedDto>>($"/api/v1/restaurants/{seed.RestaurantId}/warehouses");
        Assert.Equal(2, both!.Count);

        using HttpResponseMessage deactivate = await AuthTestHelpers.PostJsonAsync(seed.Owner, $"/api/v1/warehouses/{seed.WarehouseB}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var activeOnly = await supervisor.GetFromJsonAsync<List<AllowedDto>>($"/api/v1/restaurants/{seed.RestaurantId}/warehouses");
        Assert.Equal(seed.WarehouseA, Assert.Single(activeOnly!).Id);

        using HttpResponseMessage createAgainstInactive = await AuthTestHelpers.PostJsonAsync(
            supervisor, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseB });
        Assert.Equal(HttpStatusCode.Conflict, createAgainstInactive.StatusCode);
    }

    [Fact]
    public async Task Supervisor_Cannot_Read_Another_Restaurants_Allowed_Warehouses()
    {
        Seed seed = await SeedAsync();
        using HttpResponseMessage otherResponse = await AuthTestHelpers.PostJsonAsync(
            seed.Owner, "/api/v1/restaurants", new { nameArabic = "فرع آخر", code = "BR-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        Guid other = (await otherResponse.Content.ReadFromJsonAsync<IdDto>())!.Id;
        using HttpClient supervisor = await SupervisorAsync(seed, seed.RestaurantId);

        using HttpResponseMessage response = await supervisor.GetAsync($"/api/v1/restaurants/{other}/warehouses");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.RestaurantSupervisor)]
    [InlineData(RoleName.User)]
    public async Task Only_Owner_And_Admin_Can_Change_The_Allowed_Set(RoleName role)
    {
        Seed seed = await SeedAsync();
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/restaurants/{seed.RestaurantId}/warehouses", new { warehouseIds = new[] { seed.WarehouseA } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_Can_Change_The_Allowed_Set()
    {
        Seed seed = await SeedAsync();
        (User admin, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, admin, password);

        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(
            client, HttpMethod.Put, $"/api/v1/restaurants/{seed.RestaurantId}/warehouses", new { warehouseIds = new[] { seed.WarehouseA, seed.WarehouseB } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<List<AllowedDto>>())!.Count);
    }

    [Fact]
    public async Task Setting_The_Allowed_Set_Replaces_It_And_Removed_Warehouses_Stop_Working()
    {
        Seed seed = await SeedAsync();
        await AuthTestHelpers.AllowWarehouseAsync(seed.Owner, seed.RestaurantId, seed.WarehouseA, seed.WarehouseB);
        await AuthTestHelpers.AllowWarehouseAsync(seed.Owner, seed.RestaurantId, seed.WarehouseB);

        using HttpResponseMessage removed = await AuthTestHelpers.PostJsonAsync(
            seed.Owner, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseA });
        Assert.Equal(HttpStatusCode.Conflict, removed.StatusCode);

        using HttpResponseMessage kept = await AuthTestHelpers.PostJsonAsync(
            seed.Owner, "/api/v1/supply-requests", new { restaurantId = seed.RestaurantId, warehouseId = seed.WarehouseB });
        Assert.Equal(HttpStatusCode.Created, kept.StatusCode);
    }

    [Fact]
    public async Task Another_Companys_Or_An_Inactive_Warehouse_Cannot_Be_Added_To_The_Set()
    {
        Seed seed = await SeedAsync();
        Seed other = await SeedAsync();

        using HttpResponseMessage foreign = await AuthTestHelpers.SendJsonAsync(
            seed.Owner, HttpMethod.Put, $"/api/v1/restaurants/{seed.RestaurantId}/warehouses", new { warehouseIds = new[] { other.WarehouseA } });
        Assert.Equal(HttpStatusCode.Conflict, foreign.StatusCode);

        await AuthTestHelpers.PostJsonAsync(seed.Owner, $"/api/v1/warehouses/{seed.WarehouseB}/deactivate", new { });
        using HttpResponseMessage inactive = await AuthTestHelpers.SendJsonAsync(
            seed.Owner, HttpMethod.Put, $"/api/v1/restaurants/{seed.RestaurantId}/warehouses", new { warehouseIds = new[] { seed.WarehouseB } });
        Assert.Equal(HttpStatusCode.Conflict, inactive.StatusCode);

        var current = await seed.Owner.GetFromJsonAsync<List<AllowedDto>>($"/api/v1/restaurants/{seed.RestaurantId}/warehouses");
        Assert.Empty(current!);
    }

    [Fact]
    public async Task A_Mapping_Between_Different_Companies_Is_Impossible_At_The_Database_Level()
    {
        Seed seed = await SeedAsync();
        Seed other = await SeedAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        context.RestaurantWarehouses.Add(new RestaurantWarehouse(seed.CompanyId, seed.RestaurantId, other.WarehouseA));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
