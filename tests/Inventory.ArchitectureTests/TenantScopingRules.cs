using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Inventory.ArchitectureTests;

/// <summary>
/// Tasks 2.29-2.30. Builds the real EF model (no live database connection needed - building the
/// model does not open one) and inspects it directly, so these rules fail the instant a new
/// entity or relationship is added without the tenant machinery, rather than waiting for a
/// security test to notice a leak.
/// </summary>
public sealed class TenantScopingRules
{
    private sealed class NullCurrentUserService : ICurrentUserService
    {
        public Guid CompanyId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
    }

    private static IModel BuildModel()
    {
        DbContextOptions<InventoryDbContext> options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=architecture_test_never_connects")
            .Options;

        using var context = new InventoryDbContext(options, new NullCurrentUserService());
        return context.Model;
    }

    /// <summary>Task 2.29 - every ITenantScopedEntity has a global query filter.</summary>
    [Fact]
    public void Every_Tenant_Scoped_Entity_Has_A_Global_Query_Filter()
    {
        IModel model = BuildModel();
        var offenders = new List<string>();

        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            if (entityType.GetDeclaredQueryFilters().Count == 0)
            {
                offenders.Add(entityType.ClrType.Name);
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Every ITenantScopedEntity must have a global query filter (docs/06 section 6.7). Missing on: {string.Join(", ", offenders)}.");
    }

    /// <summary>
    /// Task 2.30 - every foreign key between two tenant-scoped entities is composite on
    /// company_id, never a single-column reference (ADR-016, docs/29 DB-03).
    /// </summary>
    [Fact]
    public void Every_Foreign_Key_Between_Tenant_Scoped_Entities_Is_Composite()
    {
        IModel model = BuildModel();
        var offenders = new List<string>();

        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            foreach (IForeignKey foreignKey in entityType.GetForeignKeys())
            {
                bool principalIsTenantScoped = typeof(ITenantScopedEntity).IsAssignableFrom(foreignKey.PrincipalEntityType.ClrType);

                if (!principalIsTenantScoped)
                {
                    continue;
                }

                bool hasCompanyIdInForeignKey = foreignKey.Properties
                    .Any(static p => string.Equals(p.Name, "CompanyId", StringComparison.Ordinal));

                if (foreignKey.Properties.Count < 2 || !hasCompanyIdInForeignKey)
                {
                    string columns = string.Join("+", foreignKey.Properties.Select(static p => p.Name));
                    offenders.Add($"{entityType.ClrType.Name} -> {foreignKey.PrincipalEntityType.ClrType.Name} ({columns})");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Every FK between two tenant-scoped entities must be composite on CompanyId (ADR-016, docs/29 DB-03). Offenders: {string.Join("; ", offenders)}.");
    }

    /// <summary>DB-02: every tenant-scoped table carries the (company_id, id) alternate key
    /// composite FKs target.</summary>
    [Fact]
    public void Every_Tenant_Scoped_Entity_Has_The_Company_Id_Alternate_Key()
    {
        IModel model = BuildModel();
        var offenders = new List<string>();

        foreach (IEntityType entityType in model.GetEntityTypes())
        {
            if (!typeof(ITenantScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            bool hasAlternateKey = entityType.GetKeys()
                .Any(key => !key.IsPrimaryKey()
                    && key.Properties.Any(p => string.Equals(p.Name, "CompanyId", StringComparison.Ordinal)));

            if (!hasAlternateKey)
            {
                offenders.Add(entityType.ClrType.Name);
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Every ITenantScopedEntity must have a UNIQUE (company_id, id) alternate key (docs/29 DB-02). Missing on: {string.Join(", ", offenders)}.");
    }
}
