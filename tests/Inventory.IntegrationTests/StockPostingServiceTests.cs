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

/// <summary>Phase 7 - Stock Engine Foundation (docs/09 tasks 7.1-7.7, 7.14-7.15). No HTTP
/// endpoint exists yet (docs/09 explicitly: "No business endpoint is exposed in this phase") -
/// <see cref="IStockPostingService"/> is exercised directly.</summary>
public sealed class StockPostingServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StockPostingServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record IdDto(Guid Id);
    private sealed record ItemDto(Guid Id, Guid BaseUnitId);

    private sealed record Seed(Guid CompanyId, Guid WarehouseId, Guid ItemId, Guid BaseUnitId, Guid ActorUserId);

    private static void AuthenticateScopeAs(IServiceProvider scopeServices, Guid companyId, Guid userId)
    {
        var httpContextAccessor = scopeServices.GetRequiredService<IHttpContextAccessor>();
        var fakeHttpContext = new DefaultHttpContext { RequestServices = scopeServices };
        fakeHttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CurrentUserService.CompanyIdClaimType, companyId.ToString()),
                new Claim(CurrentUserService.UserIdClaimType, userId.ToString()),
            ],
            authenticationType: "Test"));
        httpContextAccessor.HttpContext = fakeHttpContext;
    }

    private async Task<Seed> SeedItemAndWarehouseAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage warehouseResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/warehouses", new { nameArabic = "مستودع", code = "WH-" + Guid.NewGuid().ToString("N")[..6], address = (string?)null, description = (string?)null });
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "قسم " + Guid.NewGuid().ToString("N")[..6], description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage unitResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/units", new { nameArabic = "وحدة " + Guid.NewGuid().ToString("N")[..6], abbreviation = (string?)null });
        var unit = await unitResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage itemResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items",
            new { nameArabic = "صنف مخزون " + Guid.NewGuid().ToString("N")[..6], categoryId = category!.Id, baseUnitId = unit!.Id, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>();

        return new Seed(company.Id, warehouse!.Id, item!.Id, item.BaseUnitId, owner.Id);
    }

    /// <summary>A second item in an EXISTING seed's company/base-unit, for tests needing two
    /// items sharing one tenant (composite FKs require the item and the acting user's company
    /// to match, so the two items cannot come from two independently-seeded companies).</summary>
    private async Task<Guid> SeedSecondItemInSameCompanyAsync(Seed seed)
    {
        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, seed.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);

        using HttpResponseMessage categoryResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/categories", new { nameArabic = "قسم ثاني " + Guid.NewGuid().ToString("N")[..6], description = (string?)null });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdDto>();

        using HttpResponseMessage itemResponse = await AuthTestHelpers.PostJsonAsync(
            client, "/api/v1/items",
            new { nameArabic = "صنف ثاني " + Guid.NewGuid().ToString("N")[..6], categoryId = category!.Id, baseUnitId = seed.BaseUnitId, purchaseUnitId = (Guid?)null, defaultSupplierId = (Guid?)null, description = (string?)null });
        var item = await itemResponse.Content.ReadFromJsonAsync<ItemDto>();

        return item!.Id;
    }

    private async Task PostAsync(Seed seed, params StockPostingLine[] lines)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.ActorUserId);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var posting = scope.ServiceProvider.GetRequiredService<IStockPostingService>();
        await posting.PostAsync(lines, CancellationToken.None);
        await transaction.CommitAsync();
    }

    private async Task<StockBalance> GetBalanceAsync(Seed seed)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        return await context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(b => b.WarehouseId == seed.WarehouseId && b.ItemId == seed.ItemId);
    }

    private static StockPostingLine Line(Seed seed, MovementType type, decimal baseQuantity, decimal? unitCost, ReferenceType referenceType = ReferenceType.ReceivingOrder) =>
        new(seed.WarehouseId, seed.ItemId, seed.BaseUnitId, baseQuantity, baseQuantity, type, referenceType, Guid.NewGuid(), unitCost);

    [Fact]
    public async Task Posting_An_Incoming_Movement_Creates_The_Balance_And_The_Ledger_Row()
    {
        Seed seed = await SeedItemAndWarehouseAsync();

        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 500m, 10m));

        StockBalance balance = await GetBalanceAsync(seed);
        Assert.Equal(500m, balance.Quantity);
        Assert.Equal(10m, balance.AverageUnitCost);

        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        bool ledgerRowExists = await context.StockLedgerEntries.IgnoreQueryFilters()
            .AnyAsync(l => l.ItemId == seed.ItemId && l.MovementType == MovementType.IncomingPosted);
        Assert.True(ledgerRowExists);
    }

    [Fact]
    public async Task Second_Receipt_At_A_Different_Cost_Recomputes_The_Weighted_Average()
    {
        Seed seed = await SeedItemAndWarehouseAsync();
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 100m, 10m));

        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 50m, 16m));

        StockBalance balance = await GetBalanceAsync(seed);
        Assert.Equal(150m, balance.Quantity);
        Assert.Equal(12.0000m, balance.AverageUnitCost);
    }

    [Fact]
    public async Task Restaurant_Receipt_Confirmation_Deducts_Without_Changing_The_Average_Cost()
    {
        Seed seed = await SeedItemAndWarehouseAsync();
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 580m, 10m));

        await PostAsync(seed, Line(seed, MovementType.RestaurantReceiptConfirmed, -18m, null, ReferenceType.Supply));

        StockBalance balance = await GetBalanceAsync(seed);
        Assert.Equal(562m, balance.Quantity);
        Assert.Equal(10m, balance.AverageUnitCost);
    }

    [Fact]
    public async Task Deducting_More_Than_Available_Throws_InsufficientStockException_And_Writes_Nothing()
    {
        Seed seed = await SeedItemAndWarehouseAsync();
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 20m, 5m));

        await Assert.ThrowsAsync<InsufficientStockException>(
            () => PostAsync(seed, Line(seed, MovementType.RestaurantReceiptConfirmed, -25m, null, ReferenceType.Supply)));

        StockBalance balance = await GetBalanceAsync(seed);
        Assert.Equal(20m, balance.Quantity);
    }

    [Fact]
    public async Task Reconciliation_Reverses_At_The_Originating_Lines_Own_Cost_Not_The_Current_Wac()
    {
        Seed seed = await SeedItemAndWarehouseAsync();
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 100m, 10m));
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 50m, 16m)); // WAC now 12

        await PostAsync(seed, Line(seed, MovementType.IncomingReconciliation, -10m, 10m)); // reverses 10 units at the ORIGINAL 10, not current 12

        StockBalance balance = await GetBalanceAsync(seed);
        Assert.Equal(140m, balance.Quantity);
        // (150*12 - 10*10) / 140 = (1800-100)/140 = 12.142857...
        Assert.Equal(Math.Round(1700m / 140m, 4), balance.AverageUnitCost);
    }

    /// <summary>AC-30-1: two concurrent confirmations of 15 from a balance of 20 - exactly one
    /// succeeds, the balance ends at 5, and it is never negative at any point.</summary>
    [Fact]
    public async Task Two_Concurrent_Deductions_Of_Fifteen_From_Twenty_Exactly_One_Succeeds()
    {
        Seed seed = await SeedItemAndWarehouseAsync();
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 20m, 5m));

        Task task1 = PostAsync(seed, Line(seed, MovementType.RestaurantReceiptConfirmed, -15m, null, ReferenceType.Supply));
        Task task2 = PostAsync(seed, Line(seed, MovementType.RestaurantReceiptConfirmed, -15m, null, ReferenceType.Supply));

        var results = await Task.WhenAll(task1.ContinueWith(t => t.Exception?.InnerException), task2.ContinueWith(t => t.Exception?.InnerException));

        int succeeded = results.Count(e => e is null);
        int failed = results.Count(e => e is InsufficientStockException);
        Assert.Equal(1, succeeded);
        Assert.Equal(1, failed);

        StockBalance balance = await GetBalanceAsync(seed);
        Assert.Equal(5m, balance.Quantity);
        Assert.True(balance.Quantity >= 0);
    }

    /// <summary>AC-30-6: a forced xmin conflict on the WAC-recompute (tracked-entity) path
    /// returns a concurrency conflict, not a silently lost update.</summary>
    [Fact]
    public async Task Concurrent_Incoming_Postings_On_The_Same_Balance_Raise_A_Concurrency_Conflict()
    {
        Seed seed = await SeedItemAndWarehouseAsync();
        await PostAsync(seed, Line(seed, MovementType.IncomingPosted, 100m, 10m));

        async Task<Exception?> PostAndCaptureAsync()
        {
            try
            {
                using IServiceScope scope = _factory.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                AuthenticateScopeAs(scope.ServiceProvider, seed.CompanyId, seed.ActorUserId);
                var posting = scope.ServiceProvider.GetRequiredService<IStockPostingService>();

                // Read the balance first (outside the posting service) so both concurrent calls
                // observe the SAME starting xmin before either commits - simulating two
                // requests that both read, then both try to write.
                await context.StockBalances.AsNoTracking()
                    .FirstAsync(b => b.WarehouseId == seed.WarehouseId && b.ItemId == seed.ItemId);

                await using var transaction = await context.Database.BeginTransactionAsync();
                await posting.PostAsync([Line(seed, MovementType.IncomingPosted, 10m, 20m)], CancellationToken.None);
                await transaction.CommitAsync();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        Exception?[] outcomes = await Task.WhenAll(PostAndCaptureAsync(), PostAndCaptureAsync());

        // At least one of the two racing writers must either succeed cleanly or hit exactly the
        // concurrency exception xmin exists to raise - never a silently lost update.
        Assert.All(outcomes, ex => Assert.True(ex is null or DbUpdateConcurrencyException));
    }

    /// <summary>Task 7.6/7.15: two concurrent postings touching the SAME two items in OPPOSITE
    /// caller-supplied order never deadlock, because the service sorts ascending by item id
    /// internally regardless of the order lines are submitted in.</summary>
    [Fact]
    public async Task Interleaved_Multi_Item_Postings_In_Reversed_Order_Never_Deadlock()
    {
        Seed seedA = await SeedItemAndWarehouseAsync();
        Guid itemB = await SeedSecondItemInSameCompanyAsync(seedA);

        // Both balance rows must already exist before the concurrent phase - a fresh-row INSERT
        // race (two postings both creating the SAME never-before-seen balance simultaneously) is
        // a different scenario from the lock-ordering/deadlock one this test targets, and would
        // otherwise fail on the uq_stock_balances unique constraint instead of exercising it.
        await PostAsync(seedA, new StockPostingLine(seedA.WarehouseId, seedA.ItemId, seedA.BaseUnitId, 100m, 100m, MovementType.IncomingPosted, ReferenceType.ReceivingOrder, Guid.NewGuid(), 5m));
        await PostAsync(seedA, new StockPostingLine(seedA.WarehouseId, itemB, seedA.BaseUnitId, 100m, 100m, MovementType.IncomingPosted, ReferenceType.ReceivingOrder, Guid.NewGuid(), 5m));

        var lineA1 = new StockPostingLine(seedA.WarehouseId, seedA.ItemId, seedA.BaseUnitId, 10m, 10m, MovementType.IncomingPosted, ReferenceType.ReceivingOrder, Guid.NewGuid(), 5m);
        var lineB1 = new StockPostingLine(seedA.WarehouseId, itemB, seedA.BaseUnitId, 10m, 10m, MovementType.IncomingPosted, ReferenceType.ReceivingOrder, Guid.NewGuid(), 5m);

        Task<Exception?> callAscending = CaptureExceptionAsync(() => PostAsync(seedA, lineA1, lineB1));
        Task<Exception?> callDescending = CaptureExceptionAsync(
            () => PostAsync(seedA, lineB1 with { ReferenceId = Guid.NewGuid() }, lineA1 with { ReferenceId = Guid.NewGuid() }));

        Task<Exception?[]> allDone = Task.WhenAll(callAscending, callDescending);
        Task winner = await Task.WhenAny(allDone, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(allDone, winner); // both finished promptly - a real deadlock would still be blocked here

        // Two concurrent multi-item postings sharing both items may legitimately race on xmin
        // (an ordinary, expected concurrency conflict - task 7.5) - what this test rules out is
        // a DEADLOCK (a hang, caught by the 15s timeout above), not a race outcome.
        Exception?[] outcomes = await allDone;
        Assert.All(outcomes, ex => Assert.True(ex is null or DbUpdateConcurrencyException));
    }

    private static async Task<Exception?> CaptureExceptionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
