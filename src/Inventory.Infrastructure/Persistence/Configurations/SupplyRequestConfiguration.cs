using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class SupplyRequestConfiguration : IEntityTypeConfiguration<SupplyRequest>
{
    public void Configure(EntityTypeBuilder<SupplyRequest> builder)
    {
        builder.ToTable("supply_requests");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.RequestedBy).HasColumnName("requested_by");
        builder.Property(e => e.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.DocumentNumber }).IsUnique().HasDatabaseName("uq_req_company_doc");
        builder.HasIndex(e => new { e.CompanyId, e.WarehouseId, e.Status, e.RequestedAt })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_reqs_wh_status");
        builder.HasIndex(e => new { e.CompanyId, e.RestaurantId, e.Status, e.RequestedAt })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_reqs_rest_status");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_supply_requests_tenant_keyset");

        builder.HasOne<Restaurant>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RestaurantId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_req_warehouse");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RequestedBy })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_req_requested_by");
    }
}
