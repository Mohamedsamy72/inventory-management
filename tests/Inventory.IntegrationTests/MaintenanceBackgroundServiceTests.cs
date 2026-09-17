using Inventory.Infrastructure.Idempotency;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>Task 7.11 - expired idempotency-record cleanup and audit-partition pre-creation.</summary>
public sealed class MaintenanceBackgroundServiceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MaintenanceBackgroundServiceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static MaintenanceBackgroundService GetService(IServiceProvider services) =>
        services.GetServices<IHostedService>().OfType<MaintenanceBackgroundService>().Single();

    [Fact]
    public async Task Running_One_Pass_Creates_Next_Months_Audit_Partition()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MaintenanceBackgroundService service = GetService(scope.ServiceProvider);

        await service.RunOnceAsync(CancellationToken.None);

        DateTimeOffset nextMonth = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        string expectedPartitionName = $"audit_logs_{nextMonth:yyyy_MM}";

        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        List<string> partitionExists = await context.Database.SqlQuery<string>(
                $"SELECT to_regclass({expectedPartitionName})::text AS \"Value\"")
            .ToListAsync();

        Assert.NotNull(partitionExists[0]);
    }

    [Fact]
    public async Task Running_A_Pass_Twice_Does_Not_Fail_On_An_Already_Existing_Partition()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        MaintenanceBackgroundService service = GetService(scope.ServiceProvider);

        await service.RunOnceAsync(CancellationToken.None);
        await service.RunOnceAsync(CancellationToken.None); // must not throw
    }
}
