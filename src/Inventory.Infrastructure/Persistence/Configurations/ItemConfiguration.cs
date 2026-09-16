using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        // docs/29 section 4.3: backs the composite FK item_unit_conversions uses to enforce
        // "a conversion must target the item's own base unit" (ADR-023).
        builder.HasAlternateKey("CompanyId", "Id", "BaseUnitId").HasName("uq_items_base_unit");

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.GeneratedCode).HasColumnName("generated_code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.NameArabic).HasColumnName("name_arabic").HasMaxLength(200).IsRequired();
        builder.Property(e => e.NameNormalized).HasColumnName("name_normalized").HasMaxLength(200).IsRequired();
        builder.Property(e => e.CategoryId).HasColumnName("category_id");
        builder.Property(e => e.BaseUnitId).HasColumnName("base_unit_id");
        builder.Property(e => e.PurchaseUnitId).HasColumnName("purchase_unit_id");
        builder.Property(e => e.DefaultSupplierId).HasColumnName("default_supplier_id");
        builder.Property(e => e.Description).HasColumnName("description");
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.CompanyId, e.GeneratedCode }).IsUnique().HasDatabaseName("uq_items_company_code");

        // docs/31 section 4.2: uniqueness is evaluated on the NORMALIZED name, not the raw
        // display value - superseding docs/29's own UNIQUE (company_id, name_arabic) wording,
        // so "جبنه" and "جبنة" cannot both be created as separate items.
        builder.HasIndex(e => new { e.CompanyId, e.NameNormalized }).IsUnique().HasDatabaseName("uq_items_company_name");

        builder.HasIndex(e => new { e.CompanyId, e.IsActive, e.CategoryId }).HasDatabaseName("ix_items_active_cat");

        // docs/29 section 5.4, as corrected by docs/31 section 4.2: the trigram index targets
        // the normalized column, not the raw display value.
        builder.HasIndex(e => e.NameNormalized)
            .HasDatabaseName("ix_items_name_trgm")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_items_tenant_keyset");

        // DB-03: every tenant-scoped FK is composite, not just the "big" documents.
        builder.HasOne<Category>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.CategoryId })
            .HasPrincipalKey(c => new { c.CompanyId, c.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.BaseUnitId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_items_base_unit");

        builder.HasOne<Unit>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.PurchaseUnitId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_items_purchase_unit")
            .IsRequired(false);

        builder.HasOne<Supplier>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.DefaultSupplierId })
            .HasPrincipalKey(s => new { s.CompanyId, s.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_items_default_supplier")
            .IsRequired(false);
    }
}
