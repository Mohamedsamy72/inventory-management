using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF's fluent API has no first-class support for <c>PARTITION BY RANGE</c> - that DDL, the
/// initial partitions, the append-only triggers, and the revoked grants (docs/29 sections 4.4,
/// 4.5) are added by hand-editing the generated InitialCreate migration (task 2.26). This
/// configuration covers everything EF CAN express: columns, indexes, and the composite tenant
/// foreign keys.
/// </summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        // PostgreSQL requires every unique constraint on a partitioned table (section 4.5:
        // PARTITION BY RANGE (created_at)) to include the partition key. Id alone remains
        // effectively globally unique in practice (a random UUID) - CreatedAt is added here
        // purely to satisfy that PostgreSQL rule, not because two rows might share an Id.
        builder.HasKey(e => new { e.Id, e.CreatedAt });
        builder.HasAlternateKey(e => new { e.CompanyId, e.Id, e.CreatedAt });

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(e => e.ActorRole).HasColumnName("actor_role").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(e => e.DescriptionArabic).HasColumnName("description_arabic").IsRequired();
        builder.Property(e => e.OldValuesJson).HasColumnName("old_values").HasColumnType("jsonb");
        builder.Property(e => e.NewValuesJson).HasColumnName("new_values").HasColumnType("jsonb");
        builder.Property(e => e.Result).HasColumnName("result").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(e => e.UserAgent).HasColumnName("user_agent");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt })
            .IsDescending(false, true).HasDatabaseName("ix_audit_tenant_time");
        builder.HasIndex(e => new { e.CompanyId, e.ActorUserId, e.CreatedAt })
            .IsDescending(false, false, true).HasDatabaseName("ix_audit_actor");
        builder.HasIndex(e => new { e.CompanyId, e.EntityType, e.EntityId }).HasDatabaseName("ix_audit_entity");

        // No FK to users(id) here on purpose: a partitioned parent table cannot be the target of
        // a plain FK from an unpartitioned child under PostgreSQL's historical restrictions, and
        // actor_user_id / entity_id must survive even a hypothetical future user purge, since the
        // audit trail's job is to outlive the data it describes. Referential validity is the
        // writer's (Phase 4 interceptor's) responsibility, not a database constraint here.
    }
}
