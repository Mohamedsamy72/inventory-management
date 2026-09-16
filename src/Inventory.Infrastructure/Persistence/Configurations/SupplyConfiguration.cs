using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class SupplyConfiguration : IEntityTypeConfiguration<Supply>
{
    public void Configure(EntityTypeBuilder<Supply> builder)
    {
        builder.ToTable("supplies");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.SupplyRequestId).HasColumnName("supply_request_id");
        builder.Property(e => e.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
        // ADR-017 canonical set: Prepared, Dispatched, Confirmed, ConfirmedWithDiscrepancy,
        // RejectedAtDelivery, Cancelled. Discrepancy is deliberately NOT a status (docs/29 DB-08).
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.PreparedBy).HasColumnName("prepared_by");
        builder.Property(e => e.PreparedAt).HasColumnName("prepared_at");
        builder.Property(e => e.DispatchedBy).HasColumnName("dispatched_by");
        builder.Property(e => e.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(e => e.ConfirmedBy).HasColumnName("confirmed_by");
        builder.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.DocumentNumber }).IsUnique().HasDatabaseName("uq_sup_company_doc");
        builder.HasIndex(e => new { e.CompanyId, e.WarehouseId, e.Status }).HasDatabaseName("ix_supplies_wh_status");
        builder.HasIndex(e => new { e.CompanyId, e.RestaurantId, e.Status, e.DispatchedAt })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_sup_rest_status");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_supplies_tenant_keyset");

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Restaurant>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RestaurantId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_supplies_restaurant");

        builder.HasOne<SupplyRequest>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.SupplyRequestId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_supplies_request")
            .IsRequired(false);
    }
}
