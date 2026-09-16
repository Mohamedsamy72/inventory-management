using Inventory.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared configuration helpers so the composite-tenant-FK pattern (ADR-016, docs/29 DB-02/03)
/// is written once and reused, instead of by hand on every tenant-scoped relationship where a
/// single omission would silently degrade to a single-column FK.
/// </summary>
internal static class ConfigurationExtensions
{
    /// <summary>DB-02: `UNIQUE (company_id, id)` on every tenant-scoped table - the target every
    /// composite tenant FK references.</summary>
    public static EntityTypeBuilder<TEntity> HasTenantAlternateKey<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ITenantScopedEntity
    {
        builder.HasAlternateKey("CompanyId", "Id");
        return builder;
    }
}
