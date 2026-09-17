using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Phase 6 - Unit Conversion System (docs/09 tasks 6.1-6.8).</summary>
public sealed class ItemUnitConversionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ItemUnitConversionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record CategoryOrUnitDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);
    private sealed record ConversionDto(Guid Id, Guid ItemId, Guid FromUnitId, Guid ToBaseUnitId, decimal ConversionFactor, bool IsActive);

    private sealed record Seed(HttpClient Client, Guid ItemId, Guid BaseUnitId, Guid CartonUnitId, Guid CompanyId);

    /// <summary>Makes a bare DI scope's <c>ICurrentUserService</c> (and therefore the tenant
    /// query filter) see the given company - needed because <see cref="IUnitConversionResolver"/>
    /// is resolved directly in these tests, not through a real authenticated HTTP request, and
    /// <c>Items</c> is tenant-scoped (same pattern as <c>AuthorizationTests.AuthenticateScopeAs</c>).</summary>
    private static void AuthenticateScopeAs(IServiceProvider scopeServices, Guid companyId)
    {
        var httpContextAccessor = scopeServices.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scopeServices };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(CurrentUserService.CompanyIdClaimType, companyId.ToString())],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;
    }

    private async Task<UnitConversionResolution> ResolveAsync(Guid companyId, Guid itemId, Guid unitId, decimal quantity)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AuthenticateScopeAs(scope.ServiceProvider, companyId);
        var resolver = scope.ServiceProvider.GetRequiredService<IUnitConversionResolver>();
        return await resolver.ResolveAsync(itemId, unitId, quantity, CancellationToken.None);
    }

    private async Task<Seed> SeedItemWithUnitsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "قسم " + Guid.NewGuid().ToString("N")[..6], description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryOrUnitDto>();

        using HttpResponseMessage baseUnitResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/units", new { nameArabic = "كيلوجرام " + Guid.NewGuid().ToString("N")[..6], abbreviation = "كجم" });
        var baseUnit = await baseUnitResponse.Content.ReadFromJsonAsync<CategoryOrUnitDto>();

        using HttpResponseMessage cartonUnitResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/units", new { nameArabic = "كرتونة " + Guid.NewGuid().ToString("N")[..6], abbreviation = "كرتونة" });
        var cartonUnit = await cartonUnitResponse.Content.ReadFromJsonAsync<CategoryOrUnitDto>();

        using HttpResponseMessage itemResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items",
            new { nameArabic = "صنف تحويل " + Guid.NewGuid().ToString("N")[..6], categoryId = category!.Id, baseUnitId = baseUnit!.Id, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>();

        return new Seed(client, item!.Id, baseUnit.Id, cartonUnit!.Id, company.Id);
    }

    [Fact]
    public async Task Creating_A_Conversion_Succeeds_And_Targets_The_Items_Own_Base_Unit()
    {
        Seed seed = await SeedItemWithUnitsAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 12m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var conversion = await response.Content.ReadFromJsonAsync<ConversionDto>();
        Assert.Equal(seed.BaseUnitId, conversion!.ToBaseUnitId);
        Assert.Equal(12m, conversion.ConversionFactor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Creating_A_Conversion_With_A_Non_Positive_Factor_Returns_400(decimal factor)
    {
        Seed seed = await SeedItemWithUnitsAsync();

        using HttpResponseMessage response = await AuthTestHelpers.PostJsonAsync(
            seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = factor });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_CONVERSION_FACTOR", body, StringComparison.Ordinal);
    }

    /// <summary>1 carton = 12 KG -> 20 cartons resolve to 240 KG (docs/09 task 6.7's headline
    /// example).</summary>
    [Fact]
    public async Task Resolver_Converts_Twenty_Cartons_At_Twelve_Kg_Each_To_Two_Hundred_Forty_Kg()
    {
        Seed seed = await SeedItemWithUnitsAsync();
        await AuthTestHelpers.PostJsonAsync(seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 12m });

        UnitConversionResolution resolution = await ResolveAsync(seed.CompanyId, seed.ItemId, seed.CartonUnitId, 20m);

        Assert.True(resolution.Succeeded);
        Assert.Equal(240m, resolution.BaseQuantity);
    }

    [Fact]
    public async Task Resolver_Returns_Identity_Quantity_For_The_Items_Own_Base_Unit()
    {
        Seed seed = await SeedItemWithUnitsAsync();

        UnitConversionResolution resolution = await ResolveAsync(seed.CompanyId, seed.ItemId, seed.BaseUnitId, 7.5m);

        Assert.True(resolution.Succeeded);
        Assert.Equal(7.5m, resolution.BaseQuantity);
    }

    /// <summary>Task 6.6: 409 CONVERSION_NOT_DEFINED when no active conversion exists for the
    /// unit - proven at the resolver level (Succeeded=false) since no consuming transactional
    /// endpoint exists yet to map it to the HTTP status (docs/27 §14 records this).</summary>
    [Fact]
    public async Task Resolver_Fails_When_No_Conversion_Is_Defined_For_The_Unit()
    {
        Seed seed = await SeedItemWithUnitsAsync();

        UnitConversionResolution resolution = await ResolveAsync(seed.CompanyId, seed.ItemId, seed.CartonUnitId, 1m);

        Assert.False(resolution.Succeeded);
    }

    [Fact]
    public async Task Different_Items_Can_Have_Different_Factors_For_The_Same_Unit_Name()
    {
        Seed seed1 = await SeedItemWithUnitsAsync();
        Seed seed2 = await SeedItemWithUnitsAsync();

        await AuthTestHelpers.PostJsonAsync(seed1.Client, $"/api/v1/items/{seed1.ItemId}/conversions", new { fromUnitId = seed1.CartonUnitId, conversionFactor = 12m });
        await AuthTestHelpers.PostJsonAsync(seed2.Client, $"/api/v1/items/{seed2.ItemId}/conversions", new { fromUnitId = seed2.CartonUnitId, conversionFactor = 24m });

        UnitConversionResolution resolution1 = await ResolveAsync(seed1.CompanyId, seed1.ItemId, seed1.CartonUnitId, 1m);
        UnitConversionResolution resolution2 = await ResolveAsync(seed2.CompanyId, seed2.ItemId, seed2.CartonUnitId, 1m);

        Assert.Equal(12m, resolution1.BaseQuantity);
        Assert.Equal(24m, resolution2.BaseQuantity);
    }

    [Fact]
    public async Task A_Six_Decimal_Factor_Applies_Without_Precision_Drift()
    {
        Seed seed = await SeedItemWithUnitsAsync();
        await AuthTestHelpers.PostJsonAsync(seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 0.123456m });

        UnitConversionResolution resolution = await ResolveAsync(seed.CompanyId, seed.ItemId, seed.CartonUnitId, 1000m);

        Assert.Equal(123.456m, resolution.BaseQuantity);
    }

    /// <summary>Task 6.5/ADR-023: creating a second conversion for the SAME (item, from-unit)
    /// pair is a correction - the old row is deactivated, not deleted, and the resolver
    /// immediately uses the new factor.</summary>
    [Fact]
    public async Task Creating_A_Second_Conversion_For_The_Same_Pair_Deactivates_The_First_And_The_Resolver_Uses_The_New_Factor()
    {
        Seed seed = await SeedItemWithUnitsAsync();

        using HttpResponseMessage first = await AuthTestHelpers.PostJsonAsync(
            seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 12m });
        var firstConversion = await first.Content.ReadFromJsonAsync<ConversionDto>();

        using HttpResponseMessage second = await AuthTestHelpers.PostJsonAsync(
            seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 15m });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondConversion = await second.Content.ReadFromJsonAsync<ConversionDto>();

        Assert.NotEqual(firstConversion!.Id, secondConversion!.Id);
        Assert.True(secondConversion.IsActive);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            ItemUnitConversion firstRow = await context.ItemUnitConversions.IgnoreQueryFilters().FirstAsync(c => c.Id == firstConversion.Id);
            Assert.False(firstRow.IsActive);
        }

        UnitConversionResolution resolution = await ResolveAsync(seed.CompanyId, seed.ItemId, seed.CartonUnitId, 1m);
        Assert.Equal(15m, resolution.BaseQuantity);
    }

    [Fact]
    public async Task Listing_Conversions_For_An_Item_Returns_Both_The_Deactivated_And_Active_Rows()
    {
        Seed seed = await SeedItemWithUnitsAsync();
        await AuthTestHelpers.PostJsonAsync(seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 12m });
        await AuthTestHelpers.PostJsonAsync(seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 15m });

        using HttpResponseMessage response = await seed.Client.GetAsync($"/api/v1/items/{seed.ItemId}/conversions");
        var conversions = await response.Content.ReadFromJsonAsync<List<ConversionDto>>();

        Assert.Equal(2, conversions!.Count);
        Assert.Single(conversions, c => c.IsActive);
        Assert.Single(conversions, c => !c.IsActive);
    }

    [Fact]
    public async Task Deactivating_A_Conversion_Directly_Succeeds()
    {
        Seed seed = await SeedItemWithUnitsAsync();
        using HttpResponseMessage created = await AuthTestHelpers.PostJsonAsync(
            seed.Client, $"/api/v1/items/{seed.ItemId}/conversions", new { fromUnitId = seed.CartonUnitId, conversionFactor = 12m });
        var conversion = await created.Content.ReadFromJsonAsync<ConversionDto>();

        using HttpResponseMessage deactivated = await AuthTestHelpers.PostJsonAsync(
            seed.Client, $"/api/v1/items/{seed.ItemId}/conversions/{conversion!.Id}/deactivate", new { });

        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var body = await deactivated.Content.ReadFromJsonAsync<ConversionDto>();
        Assert.False(body!.IsActive);
    }

    [Fact]
    public async Task A_User_Without_Conversions_Manage_Is_Forbidden()
    {
        Seed seed = await SeedItemWithUnitsAsync();

        (User plainUser, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, plainUser.Id, RoleName.User);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, plainUser, password);

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/items/{seed.ItemId}/conversions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
