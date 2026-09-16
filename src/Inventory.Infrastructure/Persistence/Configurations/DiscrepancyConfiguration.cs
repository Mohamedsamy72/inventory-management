using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class DiscrepancyConfiguration : IEntityTypeConfiguration<Discrepancy>
{
    public void Configure(EntityTypeBuilder<Discrepancy> builder)
    {
        builder.ToTable("discrepancies");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        // DB-06: document_number + reference_line_id, both missing from docs/06's original DDL.
        builder.Property(e => e.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.ReferenceType).HasColumnName("reference_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(e => e.ReferenceLineId).HasColumnName("reference_line_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.ExpectedQuantity).HasColumnName("expected_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ActualQuantity).HasColumnName("actual_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.Variance).HasColumnName("variance").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reason).HasColumnName("reason");
        builder.Property(e => e.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.DocumentNumber }).IsUnique().HasDatabaseName("uq_dsc_company_doc");
        builder.HasIndex(e => new { e.CompanyId, e.Status, e.CreatedAt })
            .IsDescending(false, false, true).HasDatabaseName("ix_disc_status");
        builder.HasIndex(e => new { e.CompanyId, e.ReferenceType, e.ReferenceId }).HasDatabaseName("ix_disc_reference");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_discrepancies_tenant_keyset");

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_disc_warehouse")
            .IsRequired(false);

        builder.HasOne<Restaurant>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RestaurantId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_disc_restaurant")
            .IsRequired(false);

        builder.HasOne<Item>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ItemId })
            .HasPrincipalKey(i => new { i.CompanyId, i.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_disc_item")
            .IsRequired(false);
    }
}
