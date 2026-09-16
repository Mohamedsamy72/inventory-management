using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockCountConfiguration : IEntityTypeConfiguration<StockCount>
{
    public void Configure(EntityTypeBuilder<StockCount> builder)
    {
        builder.ToTable("stock_counts");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.IsBlindCount).HasColumnName("is_blind_count").IsRequired();
        builder.Property(e => e.OpenedBy).HasColumnName("opened_by");
        builder.Property(e => e.ApprovedBy).HasColumnName("approved_by");
        builder.Property(e => e.OpenedAt).HasColumnName("opened_at").IsRequired();
        builder.Property(e => e.ApprovedAt).HasColumnName("approved_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.DocumentNumber }).IsUnique().HasDatabaseName("uq_cnt_company_doc");
        builder.HasIndex(e => new { e.CompanyId, e.WarehouseId, e.Status, e.OpenedAt })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_cnt_wh_status");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_stock_counts_tenant_keyset");

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
