using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class SupplyRequestItemConfiguration : IEntityTypeConfiguration<SupplyRequestItem>
{
    public void Configure(EntityTypeBuilder<SupplyRequestItem> builder)
    {
        builder.ToTable("supply_request_items", t =>
        {
            t.HasCheckConstraint("ck_sri_fulfilled", "fulfilled_quantity >= 0 AND fulfilled_quantity <= requested_quantity");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SupplyRequestId).HasColumnName("supply_request_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.RequestedQuantity).HasColumnName("requested_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.FulfilledQuantity).HasColumnName("fulfilled_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.BaseQuantity).HasColumnName("base_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes");

        builder.HasIndex(e => new { e.SupplyRequestId, e.ItemId }).IsUnique().HasDatabaseName("uq_sri_item");

        builder.HasOne<SupplyRequest>().WithMany().HasForeignKey(e => e.SupplyRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_sri_unit");
    }
}
