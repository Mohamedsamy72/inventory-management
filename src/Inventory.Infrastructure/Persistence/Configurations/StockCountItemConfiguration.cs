using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockCountItemConfiguration : IEntityTypeConfiguration<StockCountItem>
{
    public void Configure(EntityTypeBuilder<StockCountItem> builder)
    {
        builder.ToTable("stock_count_items");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.StockCountId).HasColumnName("stock_count_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.SystemQuantity).HasColumnName("system_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.PhysicalQuantity).HasColumnName("physical_quantity").HasColumnType("numeric(18,4)");
        builder.Property(e => e.Variance).HasColumnName("variance").HasColumnType("numeric(18,4)");
        builder.Property(e => e.BaseUnitId).HasColumnName("base_unit_id");
        builder.Property(e => e.Notes).HasColumnName("notes");

        builder.HasIndex(e => new { e.StockCountId, e.ItemId }).IsUnique().HasDatabaseName("uq_sci_item");

        builder.HasOne<StockCount>().WithMany().HasForeignKey(e => e.StockCountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Item>().WithMany().HasForeignKey(e => e.ItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(e => e.BaseUnitId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_sci_base_unit");
    }
}
