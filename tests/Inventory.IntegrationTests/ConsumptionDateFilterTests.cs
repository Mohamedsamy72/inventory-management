using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Consumption log date filtering + print report (docs/27 section 37). Records are inserted with an explicit
/// recorded instant (created_at) so every boundary can be pinned exactly. The filter is server-side, tenant- and
/// scope-safe, applies day boundaries in the COMPANY timezone, and the print report is the same filter.</summary>
public sealed class ConsumptionDateFilterTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConsumptionDateFilterTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed record RowDto(Guid Id, Guid RestaurantId, Guid ItemId, decimal Quantity, string? ItemName, string? UnitName, string? RestaurantName, string? RecordedByName, string? RecordedAtLocal, DateTimeOffset CreatedAt);
    private sealed record PageDto(List<RowDto> Items, string? NextCursor);
    private sealed record PeriodDto(string Kind, DateOnly? From, DateOnly? To, DateTimeOffset FromUtc, DateTimeOffset ToUtc, string TimezoneId);
    private sealed record TotalDto(Guid ItemId, string ItemName, Guid UnitId, string UnitName, decimal Quantity, int RecordCount);
    private sealed record ReportDto(PeriodDto Period, DateTimeOffset GeneratedAt, string GeneratedAtLocal, string CompanyName, Guid? RestaurantId, string? RestaurantName, List<RowDto> Rows, List<TotalDto> Totals, int TotalRecordCount, bool Truncated);

    private sealed record Ctx(Guid CompanyId, User Owner, HttpClient OwnerClient, Guid RestaurantId, Guid SecondRestaurantId, Guid ItemId, Guid UnitId);

    private async Task<Ctx> NewCompanyAsync(string? timezoneId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);

        var settings = new CompanySettings(company.Id);
        if (timezoneId is not null)
        {
            settings.ChangeTimezone(timezoneId);
        }

        context.CompanySettings.Add(settings);
        var restaurant = new Restaurant(company.Id, "فرع " + Guid.NewGuid().ToString("N")[..6], "BR-" + Guid.NewGuid().ToString("N")[..6], null, null);
        var second = new Restaurant(company.Id, "فرع ثان " + Guid.NewGuid().ToString("N")[..6], "BR-" + Guid.NewGuid().ToString("N")[..6], null, null);
        var unit = new Unit(company.Id, "كيلو " + Guid.NewGuid().ToString("N")[..6], null);
        var category = new Category(company.Id, "قسم " + Guid.NewGuid().ToString("N")[..6], null);
        context.AddRange(restaurant, second, unit, category);
        await context.SaveChangesAsync();
        var item = new Item(company.Id, "ITM-" + Guid.NewGuid().ToString("N")[..6], "صنف " + Guid.NewGuid().ToString("N")[..6], category.Id, unit.Id, null, null, null);
        context.Items.Add(item);
        await context.SaveChangesAsync();

        (User owner, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, company.Id);
        await AuthTestHelpers.AssignRoleAsync(_factory, owner.Id, RoleName.Owner);
        HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, owner, password);
        return new Ctx(company.Id, owner, client, restaurant.Id, second.Id, item.Id, unit.Id);
    }

    private async Task<Guid> AddRecordAsync(Ctx ctx, DateTimeOffset recordedAt, decimal quantity = 1m, Guid? restaurantId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var record = new ConsumptionRecord(ctx.CompanyId, restaurantId ?? ctx.RestaurantId, ctx.ItemId, quantity, ctx.UnitId, quantity, DateOnly.FromDateTime(recordedAt.UtcDateTime), ctx.Owner.Id, null);
        context.ConsumptionRecords.Add(record);
        context.Entry(record).Property(r => r.CreatedAt).CurrentValue = recordedAt;
        context.Entry(record).Property(r => r.UpdatedAt).CurrentValue = recordedAt;
        await context.SaveChangesAsync();
        return record.Id;
    }

    private static async Task<PageDto> ListAsync(HttpClient client, string query = "")
    {
        using HttpResponseMessage response = await client.GetAsync("/api/v1/consumption" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PageDto>())!;
    }

    private static async Task<ReportDto> ReportAsync(HttpClient client, string query = "")
    {
        using HttpResponseMessage response = await client.GetAsync("/api/v1/consumption/report" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReportDto>())!;
    }

    private static HashSet<Guid> Ids(PageDto page) => page.Items.Select(i => i.Id).ToHashSet();

    // 1 + 2: default = rolling 24 hours, applied on the server.
    [Fact]
    public async Task Default_View_Is_The_Rolling_Last_24_Hours_And_Excludes_Older_Records()
    {
        Ctx ctx = await NewCompanyAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid recent = await AddRecordAsync(ctx, now.AddHours(-1));
        Guid almost = await AddRecordAsync(ctx, now.AddHours(-23).AddMinutes(-30));
        Guid old = await AddRecordAsync(ctx, now.AddHours(-24).AddMinutes(-5));
        Guid muchOlder = await AddRecordAsync(ctx, now.AddDays(-9));

        PageDto page = await ListAsync(ctx.OwnerClient);

        Assert.Equal(new[] { recent, almost }.ToHashSet(), Ids(page));
        Assert.DoesNotContain(old, Ids(page));
        Assert.DoesNotContain(muchOlder, Ids(page));

        ReportDto report = await ReportAsync(ctx.OwnerClient);
        Assert.Equal("Last24Hours", report.Period.Kind);
        Assert.Equal(2, report.TotalRecordCount);
    }

    // 3: single date (from = to), day boundaries in the company timezone (Africa/Cairo = UTC+2 in January).
    [Fact]
    public async Task Single_Date_Uses_The_Company_Timezone_Boundaries_Without_Off_By_One_Errors()
    {
        Ctx ctx = await NewCompanyAsync("Africa/Cairo");
        // 2026-01-10 in Cairo = [2026-01-09T22:00Z, 2026-01-10T22:00Z).
        Guid justBefore = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 9, 21, 59, 59, TimeSpan.Zero));
        Guid startInclusive = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 9, 22, 0, 0, TimeSpan.Zero));
        Guid midday = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));
        Guid lastSecond = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 10, 21, 59, 59, TimeSpan.Zero));
        Guid endExclusive = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 10, 22, 0, 0, TimeSpan.Zero));

        PageDto page = await ListAsync(ctx.OwnerClient, "?from=2026-01-10&to=2026-01-10");

        Assert.Equal(new[] { startInclusive, midday, lastSecond }.ToHashSet(), Ids(page));
        Assert.DoesNotContain(justBefore, Ids(page));
        Assert.DoesNotContain(endExclusive, Ids(page));
        Assert.All(page.Items, i => Assert.StartsWith("2026-01-10", i.RecordedAtLocal));
    }

    // 4 + 5 + 6: inclusive range across several days.
    [Fact]
    public async Task Date_Range_Is_Inclusive_Of_Both_Ends_And_Exact_On_Boundaries()
    {
        Ctx ctx = await NewCompanyAsync("Africa/Cairo");
        Guid before = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 31, 21, 59, 59, TimeSpan.Zero));   // 31 Jan 23:59:59 Cairo
        Guid firstDay = await AddRecordAsync(ctx, new DateTimeOffset(2026, 1, 31, 22, 0, 0, TimeSpan.Zero));   // 1 Feb 00:00 Cairo
        Guid inside = await AddRecordAsync(ctx, new DateTimeOffset(2026, 2, 5, 12, 0, 0, TimeSpan.Zero));
        Guid lastDay = await AddRecordAsync(ctx, new DateTimeOffset(2026, 2, 10, 21, 59, 59, TimeSpan.Zero)); // 10 Feb 23:59:59 Cairo
        Guid after = await AddRecordAsync(ctx, new DateTimeOffset(2026, 2, 10, 22, 0, 0, TimeSpan.Zero));     // 11 Feb 00:00 Cairo

        PageDto page = await ListAsync(ctx.OwnerClient, "?from=2026-02-01&to=2026-02-10");

        Assert.Equal(new[] { firstDay, inside, lastDay }.ToHashSet(), Ids(page));
        Assert.DoesNotContain(before, Ids(page));
        Assert.DoesNotContain(after, Ids(page));

        ReportDto report = await ReportAsync(ctx.OwnerClient, "?from=2026-02-01&to=2026-02-10");
        Assert.Equal(new DateTimeOffset(2026, 1, 31, 22, 0, 0, TimeSpan.Zero), report.Period.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 2, 10, 22, 0, 0, TimeSpan.Zero), report.Period.ToUtc);
    }

    // 7: the SAME instant belongs to different calendar dates in different company timezones.
    [Fact]
    public async Task The_Company_Timezone_Decides_Which_Date_A_Record_Belongs_To()
    {
        Ctx tokyo = await NewCompanyAsync("Asia/Tokyo");            // UTC+9
        Ctx cairo = await NewCompanyAsync("Africa/Cairo");          // UTC+2 (winter)
        var instant = new DateTimeOffset(2026, 1, 10, 16, 0, 0, TimeSpan.Zero); // 11 Jan 01:00 Tokyo, 10 Jan 18:00 Cairo
        Guid tokyoRecord = await AddRecordAsync(tokyo, instant);
        Guid cairoRecord = await AddRecordAsync(cairo, instant);

        Assert.Contains(tokyoRecord, Ids(await ListAsync(tokyo.OwnerClient, "?from=2026-01-11&to=2026-01-11")));
        Assert.DoesNotContain(tokyoRecord, Ids(await ListAsync(tokyo.OwnerClient, "?from=2026-01-10&to=2026-01-10")));
        Assert.Contains(cairoRecord, Ids(await ListAsync(cairo.OwnerClient, "?from=2026-01-10&to=2026-01-10")));
        Assert.DoesNotContain(cairoRecord, Ids(await ListAsync(cairo.OwnerClient, "?from=2026-01-11&to=2026-01-11")));
    }

    // 7 (DST): Cairo's DST change happens at midnight, so 2026-04-24 has no 00:00 - the day starts at 01:00 (UTC+3 = 22:00Z).
    [Fact]
    public async Task A_Day_Whose_Midnight_Does_Not_Exist_Still_Starts_Correctly()
    {
        Ctx ctx = await NewCompanyAsync("Africa/Cairo");
        Guid previousDay = await AddRecordAsync(ctx, new DateTimeOffset(2026, 4, 23, 21, 59, 59, TimeSpan.Zero)); // 23 Apr 23:59:59 (+02)
        Guid dayStart = await AddRecordAsync(ctx, new DateTimeOffset(2026, 4, 23, 22, 0, 0, TimeSpan.Zero));      // 24 Apr 01:00 (+03)

        PageDto page = await ListAsync(ctx.OwnerClient, "?from=2026-04-24&to=2026-04-24");

        Assert.Equal(new[] { dayStart }.ToHashSet(), Ids(page));
        Assert.DoesNotContain(previousDay, Ids(page));
    }

    // 8: an empty period is a normal empty result.
    [Fact]
    public async Task An_Empty_Period_Returns_An_Empty_Page_And_A_Zero_Report()
    {
        Ctx ctx = await NewCompanyAsync();
        await AddRecordAsync(ctx, DateTimeOffset.UtcNow.AddHours(-1));

        PageDto page = await ListAsync(ctx.OwnerClient, "?from=2020-01-01&to=2020-01-31");
        ReportDto report = await ReportAsync(ctx.OwnerClient, "?from=2020-01-01&to=2020-01-31");

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
        Assert.Equal(0, report.TotalRecordCount);
        Assert.Empty(report.Rows);
        Assert.Empty(report.Totals);
    }

    // 9: keyset pagination continues to work inside a filtered window.
    [Fact]
    public async Task Pagination_Works_Inside_A_Date_Filter()
    {
        Ctx ctx = await NewCompanyAsync();
        var ids = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            ids.Add(await AddRecordAsync(ctx, new DateTimeOffset(2026, 3, 10, 8 + i, 0, 0, TimeSpan.Zero)));
        }

        await AddRecordAsync(ctx, new DateTimeOffset(2026, 3, 12, 8, 0, 0, TimeSpan.Zero)); // outside the window

        var seen = new List<Guid>();
        string? cursor = null;
        int pages = 0;
        do
        {
            PageDto page = await ListAsync(ctx.OwnerClient, $"?from=2026-03-10&to=2026-03-10&limit=2{(cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor))}");
            seen.AddRange(page.Items.Select(i => i.Id));
            cursor = page.NextCursor;
            pages++;
        }
        while (cursor is not null && pages < 10);

        Assert.Equal(3, pages);
        Assert.Equal(ids.ToHashSet(), seen.ToHashSet());
        Assert.Equal(5, seen.Count);
    }

    // 10: another company's records never appear, whatever the filter or restaurant id.
    [Fact]
    public async Task Company_Isolation_Holds_For_List_And_Report()
    {
        Ctx mine = await NewCompanyAsync();
        Ctx other = await NewCompanyAsync();
        Guid mineId = await AddRecordAsync(mine, DateTimeOffset.UtcNow.AddHours(-2));
        Guid otherId = await AddRecordAsync(other, DateTimeOffset.UtcNow.AddHours(-2));

        PageDto page = await ListAsync(mine.OwnerClient);
        Assert.Contains(mineId, Ids(page));
        Assert.DoesNotContain(otherId, Ids(page));

        PageDto viaForeignRestaurant = await ListAsync(mine.OwnerClient, $"?restaurantId={other.RestaurantId}");
        Assert.Empty(viaForeignRestaurant.Items);

        ReportDto report = await ReportAsync(mine.OwnerClient);
        Assert.Equal(1, report.TotalRecordCount);
        Assert.DoesNotContain(report.Rows, r => r.Id == otherId);
    }

    // 11 + 12: a Restaurant Supervisor only ever sees their own restaurant, list and report alike.
    [Fact]
    public async Task Restaurant_Supervisor_Scope_Is_Enforced_For_List_And_Report()
    {
        Ctx ctx = await NewCompanyAsync();
        Guid own = await AddRecordAsync(ctx, DateTimeOffset.UtcNow.AddHours(-2), restaurantId: ctx.RestaurantId);
        Guid foreign = await AddRecordAsync(ctx, DateTimeOffset.UtcNow.AddHours(-2), restaurantId: ctx.SecondRestaurantId);

        (User supervisor, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, supervisor.Id, RoleName.RestaurantSupervisor);
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            context.UserRestaurantScopes.Add(new UserRestaurantScope(ctx.CompanyId, supervisor.Id, ctx.RestaurantId));
            await context.SaveChangesAsync();
        }

        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, supervisor, password);

        PageDto page = await ListAsync(client);
        Assert.Equal(new[] { own }.ToHashSet(), Ids(page));

        ReportDto report = await ReportAsync(client);
        Assert.Equal(ctx.RestaurantId, report.RestaurantId);
        Assert.Equal(new[] { own }, report.Rows.Select(r => r.Id).ToArray());

        foreach (string path in new[] { "/api/v1/consumption", "/api/v1/consumption/report" })
        {
            using HttpResponseMessage outOfScope = await client.GetAsync($"{path}?restaurantId={ctx.SecondRestaurantId}");
            Assert.Equal(HttpStatusCode.Forbidden, outOfScope.StatusCode);
        }

        Assert.DoesNotContain(foreign, Ids(page));
    }

    // 12: roles without consumption:view cannot read either endpoint.
    [Theory]
    [InlineData(RoleName.WarehouseStaff)]
    [InlineData(RoleName.User)]
    public async Task Roles_Without_The_Permission_Are_Forbidden(RoleName role)
    {
        Ctx ctx = await NewCompanyAsync();
        (User user, string password) = await AuthTestHelpers.CreateUserInCompanyAsync(_factory, ctx.CompanyId);
        await AuthTestHelpers.AssignRoleAsync(_factory, user.Id, role);
        using HttpClient client = await AuthTestHelpers.LoginAsAsync(_factory, user, password);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/consumption")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/consumption/report")).StatusCode);
    }

    // 13: no financial fields anywhere in the responses.
    [Fact]
    public async Task No_Financial_Fields_Are_Exposed()
    {
        Ctx ctx = await NewCompanyAsync();
        await AddRecordAsync(ctx, DateTimeOffset.UtcNow.AddHours(-1), 3m);

        string list = await ctx.OwnerClient.GetStringAsync("/api/v1/consumption");
        string report = await ctx.OwnerClient.GetStringAsync("/api/v1/consumption/report");

        foreach (string body in new[] { list, report })
        {
            Assert.DoesNotContain("cost", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("price", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("value", body.Replace("valueKind", string.Empty), StringComparison.OrdinalIgnoreCase);
        }
    }

    // 14: the print report is exactly the selected filter: rows == list rows, totals == sums, period echoes the request.
    [Fact]
    public async Task The_Report_Matches_The_Selected_Filter_Exactly()
    {
        Ctx ctx = await NewCompanyAsync("Africa/Cairo");
        await AddRecordAsync(ctx, new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero), 2.5m);
        await AddRecordAsync(ctx, new DateTimeOffset(2026, 5, 4, 9, 0, 0, TimeSpan.Zero), 4m);
        await AddRecordAsync(ctx, new DateTimeOffset(2026, 5, 5, 9, 0, 0, TimeSpan.Zero), 1.5m);
        await AddRecordAsync(ctx, new DateTimeOffset(2026, 5, 20, 9, 0, 0, TimeSpan.Zero), 100m); // outside

        ReportDto report = await ReportAsync(ctx.OwnerClient, "?from=2026-05-03&to=2026-05-05");
        PageDto list = await ListAsync(ctx.OwnerClient, "?from=2026-05-03&to=2026-05-05&limit=100");

        Assert.Equal("Dates", report.Period.Kind);
        Assert.Equal(new DateOnly(2026, 5, 3), report.Period.From);
        Assert.Equal(new DateOnly(2026, 5, 5), report.Period.To);
        Assert.Equal("Africa/Cairo", report.Period.TimezoneId);
        Assert.Equal(3, report.TotalRecordCount);
        Assert.False(report.Truncated);
        Assert.Equal(Ids(list), report.Rows.Select(r => r.Id).ToHashSet());
        TotalDto total = Assert.Single(report.Totals);
        Assert.Equal(8m, total.Quantity);
        Assert.Equal(3, total.RecordCount);
        Assert.False(string.IsNullOrWhiteSpace(report.CompanyName));
        Assert.False(string.IsNullOrWhiteSpace(report.GeneratedAtLocal));
        Assert.All(report.Rows, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.ItemName));
            Assert.False(string.IsNullOrWhiteSpace(r.UnitName));
            Assert.False(string.IsNullOrWhiteSpace(r.RestaurantName));
            Assert.False(string.IsNullOrWhiteSpace(r.RecordedByName));
        });
    }

    [Theory]
    [InlineData("?from=2026-01-10")]
    [InlineData("?to=2026-01-10")]
    [InlineData("?from=2026-01-10&to=2026-01-09")]
    [InlineData("?from=2020-01-01&to=2026-01-01")]
    public async Task An_Invalid_Date_Range_Is_Rejected_By_The_Server(string query)
    {
        Ctx ctx = await NewCompanyAsync();

        foreach (string path in new[] { "/api/v1/consumption", "/api/v1/consumption/report" })
        {
            using HttpResponseMessage response = await ctx.OwnerClient.GetAsync(path + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("INVALID_DATE_RANGE", body.RootElement.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task A_Malformed_Date_Is_A_400_Not_A_500()
    {
        Ctx ctx = await NewCompanyAsync();
        using HttpResponseMessage response = await ctx.OwnerClient.GetAsync("/api/v1/consumption?from=not-a-date&to=2026-01-01");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
