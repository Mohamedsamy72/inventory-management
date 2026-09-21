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

/// <summary>stock:direct_set - Owner-held, Owner-grantable direct stock correction. The change is a ledger
/// PhysicalAdjustment (never an edit), audited, tenant-scoped, and permission-gated server-side.</summary>
public sealed class DirectStockSetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DirectStockSetTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record StockLineDto(Guid ItemId, decimal Balance, decimal InTransit, decimal Available);
    private sealed record Ctx(Guid CompanyId, HttpClient Owner, Guid WarehouseId, Guid ItemId);

    private static string Unique(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private static async Task<Guid> PostIdAsync(HttpClient c, string url, object body)
    {
        using HttpResponseMessage r = await AuthTestHelpers.PostJsonAsync(c, url, body);
        Assert.True(r.IsSuccessStatusCode, $"{url} -> {(int)r.StatusCode}");
        return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private async Task<Ctx> NewCompanyAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string pw) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, pw);
        Guid warehouse = await PostIdAsync(client, "/api/v1/warehouses", new { nameArabic = Unique("مخزن "), code = Unique("WH-"), address = (string?)null, description = (string?)null });
        Guid category = await PostIdAsync(client, "/api/v1/categories", new { nameArabic = Unique("قسم "), description = (string?)null });
        Guid unit = await PostIdAsync(client, "/api/v1/units", new { nameArabic = Unique("وحدة "), abbreviation = (string?)null });
        Guid item = await PostIdAsync(client, "/api/v1/items", new { nameArabic = Unique("صنف "), categoryId = category, baseUnitId = unit, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        return new Ctx(company.Id, client, warehouse, item);
    }

    private static Task<HttpResponseMessage> SetAsync(HttpClient c, Guid warehouse, Guid item, decimal quantity) =>
        AuthTestHelpers.SendJsonAsync(c, HttpMethod.Put, $"/api/v1/warehouses/{warehouse}/stock/{item}", new { quantity, reason = "اختبار" });

    private static async Task<StockLineDto?> LineAsync(HttpClient c, Guid warehouse, Guid item)
    {
        List<StockLineDto>? lines = await c.GetFromJsonAsync<List<StockLineDto>>($"/api/v1/warehouses/{warehouse}/stock");
        return lines!.FirstOrDefault(l => l.ItemId == item);
    }

    [Fact]
    public async Task Owner_Sets_Increases_And_Decreases_Stock_With_Ledger_And_Audit()
    {
        Ctx ctx = await NewCompanyAsync();

        using HttpResponseMessage up = await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 25m);
        Assert.Equal(HttpStatusCode.OK, up.StatusCode);
        Assert.Equal(25m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);

        using HttpResponseMessage down = await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 10m);
        Assert.Equal(HttpStatusCode.OK, down.StatusCode);
        Assert.Equal(10m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        List<decimal> deltas = await context.StockLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.CompanyId == ctx.CompanyId && e.ItemId == ctx.ItemId && e.MovementType == MovementType.PhysicalAdjustment && e.ReferenceType == ReferenceType.ManualAdjustment)
            .OrderBy(e => e.CreatedAt).Select(e => e.BaseQuantity).ToListAsync();
        Assert.Equal(new[] { 25m, -15m }, deltas);
        Assert.Equal(2, await context.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.CompanyId == ctx.CompanyId && a.Action == "STOCK_DIRECTLY_SET"));
    }

    [Fact]
    public async Task Setting_The_Same_Quantity_Again_Posts_Nothing()
    {
        Ctx ctx = await NewCompanyAsync();
        await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 7m);
        using HttpResponseMessage again = await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 7m);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(1, await context.StockLedgerEntries.IgnoreQueryFilters().CountAsync(e => e.CompanyId == ctx.CompanyId && e.ItemId == ctx.ItemId));
    }

    [Fact]
    public async Task Negative_Quantity_Is_Rejected_And_Unknown_Ids_Are_404()
    {
        Ctx ctx = await NewCompanyAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, -1m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(ctx.Owner, Guid.NewGuid(), ctx.ItemId, 1m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(ctx.Owner, ctx.WarehouseId, Guid.NewGuid(), 1m)).StatusCode);
    }

    [Fact]
    public async Task Cross_Company_Warehouse_Or_Item_Is_404()
    {
        Ctx mine = await NewCompanyAsync();
        Ctx other = await NewCompanyAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(mine.Owner, other.WarehouseId, mine.ItemId, 5m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SetAsync(mine.Owner, mine.WarehouseId, other.ItemId, 5m)).StatusCode);
    }

    [Theory]
    [InlineData(RoleName.Admin)]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.RestaurantSupervisor)]
    [InlineData(RoleName.User)]
    public async Task Without_The_Permission_Every_Other_Role_Gets_403_And_Nothing_Changes(RoleName role)
    {
        Ctx ctx = await NewCompanyAsync();
        (User user, string pw) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, pw);

        Assert.Equal(HttpStatusCode.Forbidden, (await SetAsync(client, ctx.WarehouseId, ctx.ItemId, 99m)).StatusCode);
        Assert.Null(await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId));
    }

    [Fact]
    public async Task Owner_Can_Grant_The_Permission_To_A_User_Who_Then_Can_Set_Stock()
    {
        Ctx ctx = await NewCompanyAsync();
        (User user, string pw) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, RoleName.User);

        using HttpResponseMessage grant = await AuthTestHelpers.SendJsonAsync(ctx.Owner, HttpMethod.Put, $"/api/v1/users/{user.Id}/permissions",
            new { permissions = new[] { new { code = "stock:direct_set", isGranted = true }, new { code = "receiving:view", isGranted = true } } });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, pw);
        Assert.Equal(HttpStatusCode.OK, (await SetAsync(client, ctx.WarehouseId, ctx.ItemId, 12m)).StatusCode);
        Assert.Equal(12m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);
    }

    [Fact]
    public async Task Admin_Cannot_Grant_The_Permission_Because_Admin_Does_Not_Hold_It()
    {
        Ctx ctx = await NewCompanyAsync();
        (User admin, string apw) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, admin.Id, RoleName.Admin);
        (User target, _) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, target.Id, RoleName.User);
        using HttpClient adminClient = await AuthTestHelpers.LoginAsAsync(_factory, admin, apw);

        using HttpResponseMessage grant = await AuthTestHelpers.SendJsonAsync(adminClient, HttpMethod.Put, $"/api/v1/users/{target.Id}/permissions",
            new { permissions = new[] { new { code = "stock:direct_set", isGranted = true } } });

        Assert.Equal(HttpStatusCode.Forbidden, grant.StatusCode);
    }

private sealed record CostLineDto(Guid ItemId, decimal Balance, decimal? AverageUnitCost);

    private static async Task<CostLineDto?> CostLineAsync(HttpClient c, Guid warehouse, Guid item)
    {
        List<CostLineDto>? lines = await c.GetFromJsonAsync<List<CostLineDto>>($"/api/v1/warehouses/{warehouse}/stock");
        return lines!.FirstOrDefault(l => l.ItemId == item);
    }

    [Fact]
    public async Task Owner_Can_Enter_A_Unit_Cost_For_An_Increase_And_The_Average_Cost_Follows()
    {
        Ctx ctx = await NewCompanyAsync();

        using HttpResponseMessage first = await AuthTestHelpers.SendJsonAsync(ctx.Owner, HttpMethod.Put, $"/api/v1/warehouses/{ctx.WarehouseId}/stock/{ctx.ItemId}", new { quantity = 10m, unitCost = 5m, reason = "افتتاحي" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        CostLineDto line = (await CostLineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!;
        Assert.Equal(10m, line.Balance);
        Assert.Equal(5m, line.AverageUnitCost);

        // +10 more at 15 -> weighted average (10*5 + 10*15) / 20 = 10.
        using HttpResponseMessage second = await AuthTestHelpers.SendJsonAsync(ctx.Owner, HttpMethod.Put, $"/api/v1/warehouses/{ctx.WarehouseId}/stock/{ctx.ItemId}", new { quantity = 20m, unitCost = 15m, reason = "إضافة" });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        CostLineDto after = (await CostLineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!;
        Assert.Equal(20m, after.Balance);
        Assert.Equal(10m, after.AverageUnitCost);
    }

    [Fact]
    public async Task A_Negative_Cost_Is_Rejected()
    {
        Ctx ctx = await NewCompanyAsync();
        using HttpResponseMessage response = await AuthTestHelpers.SendJsonAsync(ctx.Owner, HttpMethod.Put, $"/api/v1/warehouses/{ctx.WarehouseId}/stock/{ctx.ItemId}", new { quantity = 1m, unitCost = -1m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_User_Granted_Direct_Set_But_Not_Cost_Visibility_Cannot_Enter_A_Cost()
    {
        Ctx ctx = await NewCompanyAsync();
        (User user, string pw) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, RoleName.User);
        using HttpResponseMessage grant = await AuthTestHelpers.SendJsonAsync(ctx.Owner, HttpMethod.Put, $"/api/v1/users/{user.Id}/permissions",
            new { permissions = new[] { new { code = "stock:direct_set", isGranted = true } } });
        Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, pw);

        using HttpResponseMessage withCost = await AuthTestHelpers.SendJsonAsync(client, HttpMethod.Put, $"/api/v1/warehouses/{ctx.WarehouseId}/stock/{ctx.ItemId}", new { quantity = 5m, unitCost = 3m });
        Assert.Equal(HttpStatusCode.Forbidden, withCost.StatusCode);
        Assert.Null(await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId));

        // Without a cost the same user may still set quantity.
        using HttpResponseMessage withoutCost = await AuthTestHelpers.SendJsonAsync(client, HttpMethod.Put, $"/api/v1/warehouses/{ctx.WarehouseId}/stock/{ctx.ItemId}", new { quantity = 5m });
        Assert.Equal(HttpStatusCode.OK, withoutCost.StatusCode);
    }

private const string OwnerPassword = "Str0ng!Passw0rd"; // AuthTestHelpers.CreateUserInCompanyAsync's fixed password

    private static Task<HttpResponseMessage> RemoveAsync(HttpClient c, Guid warehouse, Guid item, string? password) =>
        AuthTestHelpers.PostJsonAsync(c, $"/api/v1/warehouses/{warehouse}/stock/{item}/remove", new { password, reason = "تالف" });

    [Fact]
    public async Task Removing_An_Item_With_The_Right_Password_Zeroes_It_And_Audits_Who_And_How_Much()
    {
        Ctx ctx = await NewCompanyAsync();
        await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 37m);

        using HttpResponseMessage response = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, OwnerPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Contains(-37m, await context.StockLedgerEntries.IgnoreQueryFilters()
            .Where(e => e.CompanyId == ctx.CompanyId && e.ItemId == ctx.ItemId).Select(e => e.BaseQuantity).ToListAsync());

        AuditLog audit = await context.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.CompanyId == ctx.CompanyId && a.Action == "STOCK_ITEM_REMOVED");
        Assert.Contains("Auth Test User", audit.DescriptionArabic);   // who
        Assert.Contains("37", audit.DescriptionArabic);               // how much was there
        Assert.Equal(ctx.WarehouseId, audit.WarehouseId);
        Assert.Equal(RoleName.Owner.ToString(), audit.ActorRole.ToString());
        Assert.DoesNotContain(OwnerPassword, audit.DescriptionArabic + audit.OldValuesJson + audit.NewValuesJson);
    }

    [Fact]
    public async Task A_Wrong_Or_Missing_Password_Removes_Nothing_And_Audits_Nothing()
    {
        Ctx ctx = await NewCompanyAsync();
        await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 10m);

        using HttpResponseMessage wrong = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, "not-the-password");
        using HttpResponseMessage missing = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, null);

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("PASSWORD_CONFIRMATION_FAILED", await wrong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(10m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.False(await context.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == ctx.CompanyId && a.Action == "STOCK_ITEM_REMOVED"));
    }

    [Fact]
    public async Task Repeated_Wrong_Passwords_Lock_The_Confirmation_Out_Even_For_The_Right_Password()
    {
        Ctx ctx = await NewCompanyAsync();
        await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 10m);

        for (int i = 0; i < 4; i++)
        {
            using HttpResponseMessage wrong = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, "nope" + i);
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        // The fifth failure trips the same lockout the login uses.
        using HttpResponseMessage fifth = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, "nope4");
        Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);

        using HttpResponseMessage locked = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, OwnerPassword);
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        Assert.Equal(10m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);
    }

    [Fact]
    public async Task Without_The_Permission_Removal_Is_Forbidden_Even_With_The_Correct_Password()
    {
        Ctx ctx = await NewCompanyAsync();
        await SetAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, 10m);
        (User user, string pw) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, RoleName.User);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, pw);

        using HttpResponseMessage response = await RemoveAsync(client, ctx.WarehouseId, ctx.ItemId, pw);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(10m, (await LineAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId))!.Balance);
    }

    [Fact]
    public async Task Removing_An_Item_With_No_Stock_Is_A_Quiet_No_Op_And_Cross_Company_Is_404()
    {
        Ctx ctx = await NewCompanyAsync();
        Ctx other = await NewCompanyAsync();

        using HttpResponseMessage none = await RemoveAsync(ctx.Owner, ctx.WarehouseId, ctx.ItemId, OwnerPassword);
        using HttpResponseMessage foreign = await RemoveAsync(ctx.Owner, other.WarehouseId, ctx.ItemId, OwnerPassword);

        Assert.Equal(HttpStatusCode.OK, none.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.False(await context.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.CompanyId == ctx.CompanyId && a.Action == "STOCK_ITEM_REMOVED"));
    }
}
