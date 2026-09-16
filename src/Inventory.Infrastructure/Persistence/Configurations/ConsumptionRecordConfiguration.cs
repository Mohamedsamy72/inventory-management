using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ConsumptionRecordConfiguration : IEntityTypeConfiguration<ConsumptionRecord>
{
    public void Configure(EntityTypeBuilder<ConsumptionRecord> builder)
    {
        builder.ToTable("consumption_records", t => t.HasCheckConstraint("ck_cons_quantity", "quantity > 0"));
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.BaseQuantity).HasColumnName("base_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ConsumptionDate).HasColumnName("consumption_date").IsRequired();
        builder.Property(e => e.RecordedBy).HasColumnName("recorded_by");
        builder.Property(e => e.Notes).HasColumnName("notes");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.RestaurantId, e.ConsumptionDate })
            .IsDescending(false, false, true).HasDatabaseName("ix_cons_rest_date");
        builder.HasIndex(e => new { e.CompanyId, e.ItemId, e.ConsumptionDate })
            .IsDescending(false, false, true).HasDatabaseName("ix_cons_item_date");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_consumption_records_tenant_keyset");

        builder.HasOne<Restaurant>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RestaurantId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Item>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ItemId })
            .HasPrincipalKey(i => new { i.CompanyId, i.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cons_item");
    }
}
