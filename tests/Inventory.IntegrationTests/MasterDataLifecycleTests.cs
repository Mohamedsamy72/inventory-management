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

/// <summary>Master-data lifecycle (docs/04 §4, plan 5.12): a record NEVER referenced by history may be
/// hard-deleted; a referenced one cannot (409 IN_USE, nothing changes) and is deactivated instead.
/// Every delete is permission-gated, tenant-scoped, audited, and leaves earlier audit history intact.</summary>
public sealed class MasterDataLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MasterDataLifecycleTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId, Guid CategoryId);
    private sealed record Ctx(Guid CompanyId, HttpClient Owner);

    private async Task<Ctx> NewOwnerAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        return new Ctx(company.Id, await AuthTestHelpers.LoginAsAsync(_factory, owner, password));
    }

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    private static async Task<Guid> PostIdAsync(HttpClient client, string url, object payload)
    {
        using HttpResponseMessage r = await AuthTestHelpers.PostJsonAsync(client, url, payload);
        Assert.True(r.IsSuccessStatusCode, $"{url} -> {(int)r.StatusCode}");
        return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private static Task<Guid> NewCategoryAsync(HttpClient c) => PostIdAsync(c, "/api/v1/categories", new { nameArabic = Unique("قسم "), description = (string?)null });
    private static Task<Guid> NewUnitAsync(HttpClient c) => PostIdAsync(c, "/api/v1/units", new { nameArabic = Unique("وحدة "), abbreviation = (string?)null });
    private static Task<Guid> NewSupplierAsync(HttpClient c) => PostIdAsync(c, "/api/v1/suppliers", new { nameArabic = Unique("مورد "), phone = (string?)null, address = (string?)null, notes = (string?)null });
    private static Task<Guid> NewWarehouseAsync(HttpClient c) => PostIdAsync(c, "/api/v1/warehouses", new { nameArabic = Unique("مخزن "), code = Unique("WH-"), address = (string?)null, description = (string?)null });
    private static Task<Guid> NewRestaurantAsync(HttpClient c) => PostIdAsync(c, "/api/v1/restaurants", new { nameArabic = Unique("فرع "), code = Unique("BR-"), address = (string?)null, description = (string?)null });

    private static async Task<Guid> NewItemAsync(HttpClient c, Guid categoryId, Guid unitId, Guid? supplierId = null) =>
        await PostIdAsync(c, "/api/v1/items", new { nameArabic = Unique("صنف "), categoryId, baseUnitId = unitId, purchaseUnitId = (Guid?)null, defaultSupplierId = supplierId, description = (string?)null });

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url) =>
        await AuthTestHelpers.SendJsonAsync(client, HttpMethod.Delete, url, new { });

    private async Task AssertAuditedAsync(Guid companyId, string action, Guid entityId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.True(await context.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == companyId && a.Action == action && a.EntityId == entityId));
    }

    private async Task AssertHistoryKeptAsync(Guid companyId, Guid entityId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.True(await context.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.CompanyId == companyId && a.EntityId == entityId) >= 2,
            "the entity's creation audit row must survive its deletion");
    }

    // ---------- unused records can be deleted ----------

    [Fact]
    public async Task Unused_Category_Is_Deleted_Audited_And_History_Is_Kept()
    {
        Ctx ctx = await NewOwnerAsync();
        Guid id = await NewCategoryAsync(ctx.Owner);

        using HttpResponseMessage response = await DeleteAsync(ctx.Owner, $"/api/v1/categories/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ctx.Owner.GetAsync($"/api/v1/categories/{id}")).StatusCode);
        await AssertAuditedAsync(ctx.CompanyId, "CATEGORY_DELETED", id);
        await AssertHistoryKeptAsync(ctx.CompanyId, id);
    }

    [Fact]
    public async Task Unused_Unit_Supplier_Warehouse_Restaurant_And_Item_Are_Deleted_And_Audited()
    {
        Ctx ctx = await NewOwnerAsync();
        Guid unit = await NewUnitAsync(ctx.Owner);
        Guid supplier = await NewSupplierAsync(ctx.Owner);
        Guid warehouse = await NewWarehouseAsync(ctx.Owner);
        Guid restaurant = await NewRestaurantAsync(ctx.Owner);
        Guid category = await NewCategoryAsync(ctx.Owner);
        Guid item = await NewItemAsync(ctx.Owner, category, await NewUnitAsync(ctx.Owner));

        foreach ((string url, string action, Guid id) in new[]
        {
            ($"/api/v1/units/{unit}", "UNIT_DELETED", unit),
            ($"/api/v1/suppliers/{supplier}", "SUPPLIER_DELETED", supplier),
            ($"/api/v1/warehouses/{warehouse}", "WAREHOUSE_DELETED", warehouse),
            ($"/api/v1/restaurants/{restaurant}", "RESTAURANT_DELETED", restaurant),
            ($"/api/v1/items/{item}", "ITEM_DELETED", item),
        })
        {
            using HttpResponseMessage response = await DeleteAsync(ctx.Owner, url);
            Assert.True(response.StatusCode == HttpStatusCode.NoContent, $"{url} -> {(int)response.StatusCode}");
            Assert.Equal(HttpStatusCode.NotFound, (await ctx.Owner.GetAsync(url)).StatusCode);
            await AssertAuditedAsync(ctx.CompanyId, action, id);
        }
    }

    // ---------- referenced records are protected ----------

    [Fact]
    public async Task Referenced_Master_Data_Cannot_Be_Deleted_And_Stays_Intact_But_Can_Be_Deactivated()
    {
        Ctx ctx = await NewOwnerAsync();
        Guid category = await NewCategoryAsync(ctx.Owner);
        Guid unit = await NewUnitAsync(ctx.Owner);
        Guid supplier = await NewSupplierAsync(ctx.Owner);
        Guid item = await NewItemAsync(ctx.Owner, category, unit, supplier);
        Guid warehouse = await NewWarehouseAsync(ctx.Owner);
        Guid restaurant = await NewRestaurantAsync(ctx.Owner);

        // History: a receiving order (warehouse, supplier, item, unit) and a supply request (restaurant, warehouse).
        Guid order = await PostIdAsync(ctx.Owner, "/api/v1/receiving-orders", new { warehouseId = warehouse, supplierId = supplier, businessDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        await AuthTestHelpers.PostJsonAsync(ctx.Owner, $"/api/v1/receiving-orders/{order}/lines", new { itemId = item, unitId = unit, expectedQuantity = 5m, unitCost = 1m, notes = (string?)null });
        await AuthTestHelpers.AllowWarehouseAsync(ctx.Owner, restaurant, warehouse);
        await PostIdAsync(ctx.Owner, "/api/v1/supply-requests", new { restaurantId = restaurant, warehouseId = warehouse });

        foreach ((string url, string label) in new[]
        {
            ($"/api/v1/categories/{category}", "category"),
            ($"/api/v1/units/{unit}", "unit"),
            ($"/api/v1/suppliers/{supplier}", "supplier"),
            ($"/api/v1/items/{item}", "item"),
            ($"/api/v1/warehouses/{warehouse}", "warehouse"),
            ($"/api/v1/restaurants/{restaurant}", "restaurant"),
        })
        {
            using HttpResponseMessage response = await DeleteAsync(ctx.Owner, url);
            Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{label} delete returned {(int)response.StatusCode}");
            string body = await response.Content.ReadAsStringAsync();
            Assert.Contains("IN_USE", body, StringComparison.Ordinal);
            Assert.Contains("messageAr", body, StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.OK, (await ctx.Owner.GetAsync(url)).StatusCode);
        }

        // The safe alternative still works for a referenced record.
        using HttpResponseMessage deactivate = await AuthTestHelpers.PostJsonAsync(ctx.Owner, $"/api/v1/items/{item}/deactivate", new { });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
    }

    // ---------- authorization and tenancy ----------

    [Theory]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.RestaurantSupervisor)]
    [InlineData(RoleName.User)]
    public async Task Roles_Without_The_Permission_Get_403_And_Nothing_Is_Deleted(RoleName role)
    {
        Ctx ctx = await NewOwnerAsync();
        Guid category = await NewCategoryAsync(ctx.Owner);
        Guid unit = await NewUnitAsync(ctx.Owner);
        Guid supplier = await NewSupplierAsync(ctx.Owner);
        Guid warehouse = await NewWarehouseAsync(ctx.Owner);
        Guid restaurant = await NewRestaurantAsync(ctx.Owner);
        Guid item = await NewItemAsync(ctx.Owner, await NewCategoryAsync(ctx.Owner), await NewUnitAsync(ctx.Owner));
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, password);

        foreach (string url in new[] { $"/api/v1/categories/{category}", $"/api/v1/units/{unit}", $"/api/v1/suppliers/{supplier}", $"/api/v1/warehouses/{warehouse}", $"/api/v1/restaurants/{restaurant}", $"/api/v1/items/{item}" })
        {
            using HttpResponseMessage response = await DeleteAsync(client, url);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{url} -> {(int)response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, (await ctx.Owner.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task Admin_Can_Delete_Unused_Master_Data()
    {
        Ctx ctx = await NewOwnerAsync();
        Guid category = await NewCategoryAsync(ctx.Owner);
        (User admin, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, admin, password);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(client, $"/api/v1/categories/{category}")).StatusCode);
    }

    [Fact]
    public async Task Cross_Company_And_Unknown_Ids_Return_404_And_The_Foreign_Record_Survives()
    {
        Ctx ctx = await NewOwnerAsync();
        Ctx other = await NewOwnerAsync();
        Guid foreignCategory = await NewCategoryAsync(other.Owner);
        Guid foreignWarehouse = await NewWarehouseAsync(other.Owner);

        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(ctx.Owner, $"/api/v1/categories/{foreignCategory}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(ctx.Owner, $"/api/v1/warehouses/{foreignWarehouse}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(ctx.Owner, $"/api/v1/units/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DeleteAsync(ctx.Owner, $"/api/v1/items/{Guid.NewGuid()}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await other.Owner.GetAsync($"/api/v1/categories/{foreignCategory}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.Owner.GetAsync($"/api/v1/warehouses/{foreignWarehouse}")).StatusCode);
    }

    [Fact]
    public async Task A_Delete_Without_The_Antiforgery_Token_Is_Refused()
    {
        Ctx ctx = await NewOwnerAsync();
        Guid category = await NewCategoryAsync(ctx.Owner);

        using HttpResponseMessage response = await ctx.Owner.DeleteAsync($"/api/v1/categories/{category}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ctx.Owner.GetAsync($"/api/v1/categories/{category}")).StatusCode);
    }
}
