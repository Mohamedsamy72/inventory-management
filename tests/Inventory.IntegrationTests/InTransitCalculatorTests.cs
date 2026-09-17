using System.Net.Http.Json;
using System.Security.Claims;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 7.12 (ADR-018) - no HTTP endpoint dispatches supplies yet (that is Phase 10),
/// so a <see cref="Supply"/>/<see cref="SupplyItem"/> pair is constructed directly to exercise
/// the calculator against real, tenant-scoped data.</summary>
public sealed class InTransitCalculatorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InTransitCalculatorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);

    private static void AuthenticateScopeAs(IServiceProvider scopeServices, Guid companyId)
    {
        var httpContextAccessor = scopeServices.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scopeServices };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(CurrentUserService.CompanyIdClaimType, companyId.ToString())],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;
    }

    [Fact]
    public async Task A_Dispatched_Supply_Counts_As_In_Transit_For_Its_Warehouse()
    {
        using IServiceScope setupScope = _factory.Services.CreateScope();
        var context = setupScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage warehouseResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses", new { nameArabic = "مستودع", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = warehouse!.Id, address = (string?)null, description = (string?)null });
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

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var writeContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            AuthenticateScopeAs(scope.ServiceProvider, company.Id);

            var supply = new Supply(company.Id, warehouse.Id, restaurant!.Id, supplyRequestId: null, "SUP-202609-0001", owner.Id);
            supply.Dispatch(owner.Id);
            writeContext.Supplies.Add(supply);
            writeContext.SupplyItems.Add(new SupplyItem(supply.Id, null, item!.Id, 20m, unit.Id, 20m));
            await writeContext.SaveChangesAsync();
        }

        using IServiceScope readScope = _factory.Services.CreateScope();
        AuthenticateScopeAs(readScope.ServiceProvider, company.Id);
        var calculator = readScope.ServiceProvider.GetRequiredService<IInTransitCalculator>();

        decimal inTransit = await calculator.GetInTransitQuantityAsync(warehouse.Id, item.Id, CancellationToken.None);
        Assert.Equal(20m, inTransit);

        IReadOnlyDictionary<Guid, decimal> byItem = await calculator.GetInTransitQuantitiesByItemAsync(warehouse.Id, CancellationToken.None);
        Assert.Equal(20m, byItem[item.Id]);
    }

    [Fact]
    public async Task A_Confirmed_Supply_No_Longer_Counts_As_In_Transit()
    {
        using IServiceScope setupScope = _factory.Services.CreateScope();
        var context = setupScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage warehouseResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses", new { nameArabic = "مستودع", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage restaurantResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/restaurants",
            new { nameArabic = "فرع", code = "RST-" + Guid.NewGuid().ToString("N")[..6], defaultServingWarehouseId = warehouse!.Id, address = (string?)null, description = (string?)null });
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

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var writeContext = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            AuthenticateScopeAs(scope.ServiceProvider, company.Id);

            var supply = new Supply(company.Id, warehouse.Id, restaurant!.Id, supplyRequestId: null, "SUP-202609-0002", owner.Id);
            supply.Dispatch(owner.Id);
            supply.Confirm(owner.Id, SupplyStatus.Confirmed);
            writeContext.Supplies.Add(supply);
            writeContext.SupplyItems.Add(new SupplyItem(supply.Id, null, item!.Id, 20m, unit.Id, 20m));
            await writeContext.SaveChangesAsync();
        }

        using IServiceScope readScope = _factory.Services.CreateScope();
        AuthenticateScopeAs(readScope.ServiceProvider, company.Id);
        var calculator = readScope.ServiceProvider.GetRequiredService<IInTransitCalculator>();

        decimal inTransit = await calculator.GetInTransitQuantityAsync(warehouse.Id, item.Id, CancellationToken.None);
        Assert.Equal(0m, inTransit);
    }
}
