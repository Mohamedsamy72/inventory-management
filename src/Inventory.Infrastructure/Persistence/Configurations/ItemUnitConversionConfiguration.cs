using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ItemUnitConversionConfiguration : IEntityTypeConfiguration<ItemUnitConversion>
{
    public void Configure(EntityTypeBuilder<ItemUnitConversion> builder)
    {
        builder.ToTable("item_unit_conversions");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.FromUnitId).HasColumnName("from_unit_id");
        builder.Property(e => e.ToBaseUnitId).HasColumnName("to_base_unit_id");
        builder.Property(e => e.ConversionFactor).HasColumnName("conversion_factor").HasColumnType("numeric(18,6)").IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.ItemId, e.FromUnitId, e.ToBaseUnitId }).IsUnique().HasDatabaseName("uq_item_conversion");
        builder.HasIndex(e => new { e.CompanyId, e.ItemId, e.IsActive }).HasDatabaseName("ix_conv_item");

        builder.HasOne<Unit>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.FromUnitId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_conversion_from_unit");

        // ADR-023 / CR-038: the target must be the item's OWN base unit, structurally - not
        // merely validated in application code (docs/29 section 4.3).
        builder.HasOne<Item>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ItemId, e.ToBaseUnitId })
            .HasPrincipalKey(i => new { i.CompanyId, i.Id, i.BaseUnitId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_conversion_item_base_unit");
    }
}
