using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ReceivingOrderItemConfiguration : IEntityTypeConfiguration<ReceivingOrderItem>
{
    public void Configure(EntityTypeBuilder<ReceivingOrderItem> builder)
    {
        builder.ToTable("receiving_order_items", t =>
        {
            t.HasCheckConstraint("ck_roi_base", "base_quantity > 0");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ReceivingOrderId).HasColumnName("receiving_order_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.ExpectedQuantity).HasColumnName("expected_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ActualQuantity).HasColumnName("actual_quantity").HasColumnType("numeric(18,4)");
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.BaseQuantity).HasColumnName("base_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ActualBaseQuantity).HasColumnName("actual_base_quantity").HasColumnType("numeric(18,4)");
        builder.Property(e => e.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(18,4)");
        builder.Property(e => e.TotalCost).HasColumnName("total_cost").HasColumnType("numeric(18,4)");
        builder.Property(e => e.Reconciled).HasColumnName("reconciled").IsRequired();
        builder.Property(e => e.Notes).HasColumnName("notes");

        // CR-023: one line per item per document.
        builder.HasIndex(e => new { e.ReceivingOrderId, e.ItemId }).IsUnique().HasDatabaseName("uq_roi_item");

        // docs/29 DB-15: line tables narrow to RESTRICT for non-draft parents - the application
        // layer deletes/replaces draft lines explicitly; the database does not cascade once a
        // document has left Draft.
        builder.HasOne<ReceivingOrder>().WithMany()
            .HasForeignKey(e => e.ReceivingOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_roi_unit");
    }
}
