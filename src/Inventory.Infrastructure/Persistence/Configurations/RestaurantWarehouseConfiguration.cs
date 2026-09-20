using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class RestaurantWarehouseConfiguration : IEntityTypeConfiguration<RestaurantWarehouse>
{
    public void Configure(EntityTypeBuilder<RestaurantWarehouse> builder)
    {
        builder.ToTable("restaurant_warehouses");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.RestaurantId, e.WarehouseId }).IsUnique().HasDatabaseName("uq_restaurant_warehouse");
        builder.HasIndex(e => e.WarehouseId).HasDatabaseName("ix_restaurant_warehouses_warehouse");

        // Same composite tenant FKs as every other tenant join table (ADR-016): a cross-company
        // mapping is impossible at the database level, not merely rejected in code.
        builder.HasOne<Restaurant>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RestaurantId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
