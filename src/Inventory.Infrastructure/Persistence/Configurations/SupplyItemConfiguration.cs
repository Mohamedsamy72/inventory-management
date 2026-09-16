using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class SupplyItemConfiguration : IEntityTypeConfiguration<SupplyItem>
{
    public void Configure(EntityTypeBuilder<SupplyItem> builder)
    {
        builder.ToTable("supply_items", t =>
        {
            t.HasCheckConstraint("ck_si_received", "received_quantity IS NULL OR (received_quantity >= 0 AND received_quantity <= dispatched_quantity)");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.SupplyId).HasColumnName("supply_id");
        // DB-07: without this, request -> dispatch -> receipt traceability is impossible.
        builder.Property(e => e.SupplyRequestItemId).HasColumnName("supply_request_item_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.DispatchedQuantity).HasColumnName("dispatched_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ReceivedQuantity).HasColumnName("received_quantity").HasColumnType("numeric(18,4)");
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.DispatchedBaseQuantity).HasColumnName("dispatched_base_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.ReceivedBaseQuantity).HasColumnName("received_base_quantity").HasColumnType("numeric(18,4)");
        builder.Property(e => e.Variance).HasColumnName("variance").HasColumnType("numeric(18,4)");

        // docs/29 section 5.2 also lists a plain (non-unique) ix_supply_items_supply index on
        // these exact columns - EF collapses a second index over identical columns into the
        // first, so only one is declared here; the unique constraint already serves every query
        // the plain index would have (CR-023).
        builder.HasIndex(e => new { e.SupplyId, e.ItemId }).IsUnique().HasDatabaseName("uq_si_item");

        builder.HasOne<Supply>().WithMany().HasForeignKey(e => e.SupplyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SupplyRequestItem>().WithMany().HasForeignKey(e => e.SupplyRequestItemId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(e => e.UnitId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_si_unit");
    }
}
