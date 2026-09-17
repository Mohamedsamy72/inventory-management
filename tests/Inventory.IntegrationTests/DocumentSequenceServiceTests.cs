using System.Security.Claims;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 5.16 (docs/28 §10 AC-28-1, AC-28-3, AC-28-4) - the sequence allocation engine
/// itself, exercised directly rather than through an entity-creation endpoint (no master-data
/// create endpoint exists yet at the point this test was written within Phase 5 - see the
/// sequence within docs/27 §13 for exactly which task landed first).</summary>
public sealed class DocumentSequenceServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DocumentSequenceServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

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
    public async Task Allocating_An_Item_Code_Twice_Produces_Sequential_Six_Digit_Codes()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        AuthenticateScopeAs(scope.ServiceProvider, company.Id);

        var sequenceService = scope.ServiceProvider.GetRequiredService<IDocumentSequenceService>();

        string first = await sequenceService.AllocateAsync(DocumentType.Item, CancellationToken.None);
        string second = await sequenceService.AllocateAsync(DocumentType.Item, CancellationToken.None);

        Assert.Equal("ITM-000001", first);
        Assert.Equal("ITM-000002", second);
    }

    [Fact]
    public async Task Allocating_A_Monthly_Document_Number_Uses_The_Tenant_Timezone_Business_Month()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        context.CompanySettings.Add(new CompanySettings(company.Id));
        await context.SaveChangesAsync();
        AuthenticateScopeAs(scope.ServiceProvider, company.Id);

        var sequenceService = scope.ServiceProvider.GetRequiredService<IDocumentSequenceService>();
        string number = await sequenceService.AllocateAsync(DocumentType.ReceivingOrder, CancellationToken.None);

        var cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        string expectedPeriod = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, cairo)
            .ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal($"REC-{expectedPeriod}-0001", number);
    }

    /// <summary>AC-28-3: a transaction that rolls back after allocation leaves `last_value`
    /// unchanged - proven by rolling one back and observing the NEXT allocation reuse the exact
    /// same number, not skip it.</summary>
    [Fact]
    public async Task A_Rolled_Back_Allocation_Leaves_The_Counter_Unchanged_And_The_Number_Is_Reused()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Company company = await AuthTestHelpers.CreateCompanyAsync(context);
        AuthenticateScopeAs(scope.ServiceProvider, company.Id);

        var sequenceService = scope.ServiceProvider.GetRequiredService<IDocumentSequenceService>();

        await using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync())
        {
            string discarded = await sequenceService.AllocateAsync(DocumentType.Item, CancellationToken.None);
            Assert.Equal("ITM-000001", discarded);
            await transaction.RollbackAsync();
        }

        string reused = await sequenceService.AllocateAsync(DocumentType.Item, CancellationToken.None);

        Assert.Equal("ITM-000001", reused);
    }

    /// <summary>AC-28-1: 1,000 concurrent allocations in one company/document-type yield 1,000
    /// distinct, gap-free codes - each "concurrent caller" gets its own DI scope/DbContext
    /// (DbContext is not thread-safe), simulating 1,000 simultaneous requests the way 1,000 real
    /// HTTP calls would, without the overhead of actually routing them through the pipeline.
    /// </summary>
    [Fact]
    public async Task One_Thousand_Concurrent_Allocations_Yield_One_Thousand_Distinct_Gap_Free_Codes()
    {
        Guid companyId;
        using (IServiceScope seedScope = _factory.Services.CreateScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            Company company = await AuthTestHelpers.CreateCompanyAsync(context);
            companyId = company.Id;
        }

        const int concurrency = 1000;
        var tasks = new Task<string>[concurrency];

        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = AllocateOneAsync(companyId);
        }

        string[] codes = await Task.WhenAll(tasks);

        Assert.Equal(concurrency, codes.Distinct(StringComparer.Ordinal).Count());

        var numericValues = codes
            .Select(code => int.Parse(code["ITM-".Length..], System.Globalization.CultureInfo.InvariantCulture))
            .OrderBy(v => v)
            .ToList();

        Assert.Equal(Enumerable.Range(1, concurrency), numericValues);
    }

    private async Task<string> AllocateOneAsync(Guid companyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AuthenticateScopeAs(scope.ServiceProvider, companyId);
        var sequenceService = scope.ServiceProvider.GetRequiredService<IDocumentSequenceService>();
        return await sequenceService.AllocateAsync(DocumentType.Item, CancellationToken.None);
    }
}
