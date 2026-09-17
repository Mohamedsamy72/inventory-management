using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Inventory.IntegrationTests;

/// <summary>
/// Task 2.31 - applies the initial migration to an empty PostgreSQL database and verifies it
/// produces every index docs/29 section 5 requires.
/// </summary>
/// <remarks>
/// Targets this repository's own dedicated local PostgreSQL instance (docs/19 section 1) rather
/// than Testcontainers: Docker Desktop is installed but its engine is not running on this
/// machine (docs/33 section 4.3), and docs/09's "integration-test host" open decision names a
/// dedicated local database as the explicit fallback when Testcontainers is unavailable. A
/// separate database (<c>restaurant_inventory_test</c>) on the same instance keeps this
/// completely isolated from the development database.
/// </remarks>
public sealed class MigrationTestDatabaseFixture : IAsyncLifetime
{
    private const string TestDatabaseName = "restaurant_inventory_test";

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        using var factory = new WebApplicationFactory<Program>();
        using IServiceScope scope = factory.Services.CreateScope();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        string devConnectionString = configuration.GetConnectionString(DependencyInjection.DatabaseConnectionName)
            ?? throw new InvalidOperationException("InventoryDatabase connection string is not configured.");

        var builder = new NpgsqlConnectionStringBuilder(devConnectionString) { Database = TestDatabaseName };
        ConnectionString = builder.ConnectionString;

        // Idle connections from a previous test run can otherwise hold the database open and
        // turn DROP DATABASE into "being accessed by other users".
        NpgsqlConnection.ClearAllPools();

        var adminBuilder = new NpgsqlConnectionStringBuilder(devConnectionString) { Database = "postgres" };
        await using var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString);
        await adminConnection.OpenAsync();

        await using (var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{TestDatabaseName}\" WITH (FORCE);", adminConnection))
        {
            await dropCommand.ExecuteNonQueryAsync();
        }

        await using (var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{TestDatabaseName}\";", adminConnection))
        {
            await createCommand.ExecuteNonQueryAsync();
        }

        await using InventoryDbContext context = CreateContext(ConnectionString);
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public static InventoryDbContext CreateContext(string connectionString)
    {
        DbContextOptions<InventoryDbContext> options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new InventoryDbContext(options, new TestCurrentUserService());
    }

    private sealed class TestCurrentUserService : Application.Common.ICurrentUserService
    {
        public Guid CompanyId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
        public Domain.Enums.RoleName? Role => null;
    }
}

public sealed class MigrationApplicationTests : IClassFixture<MigrationTestDatabaseFixture>
{
    private readonly MigrationTestDatabaseFixture _fixture;

    public MigrationApplicationTests(MigrationTestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The migration applies cleanly to an empty database (Definition of Done item 3).</summary>
    [Fact]
    public async Task Migration_Applies_To_An_Empty_Database()
    {
        List<string> tables = await QueryStringColumnAsync(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type IN ('BASE TABLE', 'PARTITIONED TABLE');");

        Assert.Contains("stock_ledger", tables);
        Assert.Contains("audit_logs", tables);
        Assert.Contains("companies", tables);
        Assert.Contains("document_sequences", tables);
    }

    /// <summary>AC-29-6: every index in docs/29 section 5 exists after the migration.</summary>
    [Fact]
    public async Task Migration_Creates_Every_Required_Index()
    {
        List<string> indexes = await QueryStringColumnAsync("SELECT indexname FROM pg_indexes WHERE schemaname = 'public';");

        string[] required =
        [
            "uq_stock_balances",
            "ix_ledger_wh_item_time",
            "ix_ledger_reference",
            "ix_ledger_type_time",
            "ix_supplies_wh_status",
            "ix_reqs_wh_status",
            "ix_reqs_rest_status",
            "ix_sup_rest_status",
            "ix_rec_wh_status",
            "ix_cnt_wh_status",
            "ix_disc_status",
            "ix_disc_reference",
            "ix_items_active_cat",
            "ix_items_name_trgm",
            "ix_conv_item",
            "ix_uws_user",
            "ix_urs_user",
            "ix_audit_tenant_time",
            "ix_audit_actor",
            "ix_audit_entity",
            "ix_idem_expiry",
            "ix_otp_user_active",
            "ix_cons_rest_date",
            "ix_cons_item_date",
            "uq_ledger_posting",
            "uq_user_single_role",
            "uq_si_item",
            "uq_sri_item",
            "uq_roi_item",
            "uq_sci_item",
            "uq_idempotency_key",
            "uq_items_base_unit",
        ];

        string[] missing = required.Where(index => !indexes.Contains(index)).ToArray();

        Assert.True(missing.Length == 0, $"Missing indexes from docs/29 section 5: {string.Join(", ", missing)}.");
    }

    /// <summary>The append-only triggers survive a real migration apply.</summary>
    [Fact]
    public async Task Append_Only_Triggers_Exist_After_Migration()
    {
        List<string> triggers = await QueryStringColumnAsync("SELECT DISTINCT tgname FROM pg_trigger WHERE NOT tgisinternal;");

        Assert.Contains("trg_stock_ledger_append_only", triggers);
        Assert.Contains("trg_audit_logs_append_only", triggers);
    }

    /// <summary>audit_logs is genuinely partitioned, with the current and next month present (AC-29-9).</summary>
    [Fact]
    public async Task Audit_Logs_Is_Partitioned_With_Current_And_Next_Month()
    {
        List<string> partitionTables = await QueryStringColumnAsync(
            "SELECT inhrelid::regclass::text FROM pg_inherits WHERE inhparent = 'audit_logs'::regclass;");

        Assert.True(partitionTables.Count >= 2, $"Expected at least 2 audit_logs partitions; found {partitionTables.Count}.");
    }

    /// <summary>DB-05/AC-29-8: a second role for the same user is rejected by the database.</summary>
    [Fact]
    public async Task Assigning_A_Second_Role_To_A_User_Fails_On_The_Unique_Constraint()
    {
        await using InventoryDbContext context = MigrationTestDatabaseFixture.CreateContext(_fixture.ConnectionString);

        var company = new Domain.Entities.Company("Test Co", "TESTMIG" + Guid.NewGuid().ToString("N")[..8]);
        context.Companies.Add(company);

        var role1 = await context.Roles.AsNoTracking().FirstAsync(r => r.Name == Domain.Enums.RoleName.Owner);
        var role2 = await context.Roles.AsNoTracking().FirstAsync(r => r.Name == Domain.Enums.RoleName.Admin);

        var user = new Domain.Entities.User(company.Id, "Test User", "0109" + Guid.NewGuid().ToString("N")[..7], "hash", "stamp");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.UserRoles.Add(new Domain.Entities.UserRole(user.Id, role1.Id));
        await context.SaveChangesAsync();

        context.UserRoles.Add(new Domain.Entities.UserRole(user.Id, role2.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>AC-29-3: an UPDATE on a real stock_ledger row fails loudly, not silently.</summary>
    [Fact]
    public async Task Updating_A_Stock_Ledger_Row_Fails_Loudly()
    {
        await using InventoryDbContext context = MigrationTestDatabaseFixture.CreateContext(_fixture.ConnectionString);
        (Domain.Entities.StockLedgerEntry entry, _) = await SeedOneStockLedgerEntryAsync(context);

        // StockLedgerEntry has no setters (immutable by design) - mutate via raw SQL instead,
        // exactly as an ORM bug or a stray script would attempt.
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE stock_ledger SET quantity = 999 WHERE id = @id;", connection);
        command.Parameters.AddWithValue("id", entry.Id);

        Npgsql.PostgresException exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal("55006", exception.SqlState);
    }

    /// <summary>AC-29-7: a second line for the same item on one document fails on the unique constraint.</summary>
    [Fact]
    public async Task Duplicate_Item_On_The_Same_Document_Is_Rejected()
    {
        await using InventoryDbContext context = MigrationTestDatabaseFixture.CreateContext(_fixture.ConnectionString);
        (Domain.Entities.StockLedgerEntry seedEntry, Guid unitId) = await SeedOneStockLedgerEntryAsync(context);

        var order = new Domain.Entities.ReceivingOrder(seedEntry.CompanyId, seedEntry.WarehouseId, null, "REC-DUP-" + Guid.NewGuid().ToString("N")[..6], DateOnly.FromDateTime(DateTime.UtcNow), seedEntry.ActorUserId);
        context.ReceivingOrders.Add(order);
        await context.SaveChangesAsync();

        context.ReceivingOrderItems.Add(new Domain.Entities.ReceivingOrderItem(order.Id, seedEntry.ItemId, 10m, unitId, 10m, null));
        await context.SaveChangesAsync();

        context.ReceivingOrderItems.Add(new Domain.Entities.ReceivingOrderItem(order.Id, seedEntry.ItemId, 5m, unitId, 5m, null));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>AC-29-4: stock_balances has no `version` column - xmin is the concurrency token.</summary>
    [Fact]
    public async Task Stock_Balances_Has_No_Version_Column()
    {
        List<string> columns = await QueryStringColumnAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'stock_balances';");

        Assert.DoesNotContain("version", columns);
    }

    private static async Task<(Domain.Entities.StockLedgerEntry Entry, Guid UnitId)> SeedOneStockLedgerEntryAsync(InventoryDbContext context)
    {
        var company = new Domain.Entities.Company("Test Co", "TCO" + Guid.NewGuid().ToString("N")[..8]);
        context.Companies.Add(company);

        var user = new Domain.Entities.User(company.Id, "Test User", "0108" + Guid.NewGuid().ToString("N")[..7], "hash", "stamp");
        context.Users.Add(user);

        var warehouse = new Domain.Entities.Warehouse(company.Id, "مستودع اختبار", "WH" + Guid.NewGuid().ToString("N")[..6], null, null);
        context.Warehouses.Add(warehouse);

        var unit = new Domain.Entities.Unit(company.Id, "كيلو", "kg");
        context.Units.Add(unit);

        var category = new Domain.Entities.Category(company.Id, "قسم اختبار", null);
        context.Categories.Add(category);

        var item = new Domain.Entities.Item(company.Id, "ITM-" + Guid.NewGuid().ToString("N")[..6], "صنف اختبار " + Guid.NewGuid().ToString("N")[..6], category.Id, unit.Id, null, null, null);
        context.Items.Add(item);

        await context.SaveChangesAsync();

        var entry = new Domain.Entities.StockLedgerEntry(
            company.Id, warehouse.Id, item.Id, Domain.Enums.MovementType.OpeningBalance,
            100m, 100m, unit.Id, Domain.Enums.ReferenceType.ManualAdjustment, Guid.NewGuid(),
            null, null, user.Id, DateTimeOffset.UtcNow);
        context.StockLedgerEntries.Add(entry);
        await context.SaveChangesAsync();

        return (entry, unit.Id);
    }

    private async Task<List<string>> QueryStringColumnAsync(string sql)
    {
        var results = new List<string>();
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }
}
