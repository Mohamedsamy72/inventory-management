using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances", t => t.HasCheckConstraint("ck_stock_balances_quantity", "quantity >= 0"));
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.BaseUnitId).HasColumnName("base_unit_id");
        builder.Property(e => e.AverageUnitCost).HasColumnName("average_unit_cost").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // ADR-022 / docs/29 DB-01: PostgreSQL xmin as the concurrency token - NOT a `version`
        // column, which would be a SQL Server idiom PostgreSQL has no auto-maintenance for.
        // Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 has no UseXminAsConcurrencyToken()
        // convenience method (verified: absent from the assembly) - this is the documented
        // manual equivalent: a shadow uint property mapped straight onto the system column.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.HasIndex(e => new { e.CompanyId, e.WarehouseId, e.ItemId }).IsUnique().HasDatabaseName("uq_stock_balances");

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Item>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ItemId })
            .HasPrincipalKey(i => new { i.CompanyId, i.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.BaseUnitId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_stock_balances_base_unit");
    }
}
