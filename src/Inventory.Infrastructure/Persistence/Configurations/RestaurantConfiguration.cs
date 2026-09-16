using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("restaurants");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.NameArabic).HasColumnName("name_arabic").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.DefaultServingWarehouseId).HasColumnName("default_serving_warehouse_id").IsRequired();
        builder.Property(e => e.Address).HasColumnName("address");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique().HasDatabaseName("uq_restaurants_company_code");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_restaurants_tenant_keyset");

        // ADR-028 / docs/29 section 4.2: the serving warehouse must be in the same company -
        // structural, not an application-level assertion (BR-29-10).
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.DefaultServingWarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_restaurants_serving_warehouse");
        builder.HasIndex(e => new { e.CompanyId, e.DefaultServingWarehouseId }).HasDatabaseName("ix_restaurants_serving_wh");
    }
}
