using System.Linq.Expressions;
using System.Reflection;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

/// <summary>
/// The composition root of the schema. Every <see cref="ITenantScopedEntity"/> gets a global
/// query filter comparing its CompanyId against <see cref="ICurrentUserService"/> - built once,
/// reflectively, here, rather than repeated by hand on 20+ entity configurations where a single
/// omission would be a silent cross-tenant leak (docs/06 section 6.7, ADR-016).
/// </summary>
public sealed class InventoryDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserWarehouseScope> UserWarehouseScopes => Set<UserWarehouseScope>();
    public DbSet<UserRestaurantScope> UserRestaurantScopes => Set<UserRestaurantScope>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemUnitConversion> ItemUnitConversions => Set<ItemUnitConversion>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();
    public DbSet<ReceivingOrder> ReceivingOrders => Set<ReceivingOrder>();
    public DbSet<ReceivingOrderItem> ReceivingOrderItems => Set<ReceivingOrderItem>();
    public DbSet<SupplyRequest> SupplyRequests => Set<SupplyRequest>();
    public DbSet<SupplyRequestItem> SupplyRequestItems => Set<SupplyRequestItem>();
    public DbSet<Supply> Supplies => Set<Supply>();
    public DbSet<SupplyItem> SupplyItems => Set<SupplyItem>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountItem> StockCountItems => Set<StockCountItem>();
    public DbSet<Discrepancy> Discrepancies => Set<Discrepancy>();
    public DbSet<ConsumptionRecord> ConsumptionRecords => Set<ConsumptionRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<PasswordResetOtp> PasswordResetOtps => Set<PasswordResetOtp>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // docs/29 section 5.4: Arabic substring search on items.name_normalized.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);

        MethodInfo applyFilterMethod = typeof(InventoryDbContext)
            .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                applyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    /// <summary>Builds `e => e.CompanyId == _currentUserService.CompanyId` for one entity type.
    /// A closed-over service reference is intentional: the filter must re-evaluate per query,
    /// not bake in the tenant at model-build time.</summary>
    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScopedEntity
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression companyIdProperty = Expression.Property(parameter, nameof(ITenantScopedEntity.CompanyId));
        MemberExpression currentCompanyId = Expression.Property(
            Expression.Constant(_currentUserService),
            nameof(ICurrentUserService.CompanyId));
        BinaryExpression equals = Expression.Equal(companyIdProperty, currentCompanyId);
        Expression<Func<TEntity, bool>> lambda = Expression.Lambda<Func<TEntity, bool>>(equals, parameter);

        modelBuilder.Entity<TEntity>().HasQueryFilter(lambda);
    }
}
