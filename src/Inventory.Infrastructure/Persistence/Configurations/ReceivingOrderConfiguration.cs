using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ReceivingOrderConfiguration : IEntityTypeConfiguration<ReceivingOrder>
{
    public void Configure(EntityTypeBuilder<ReceivingOrder> builder)
    {
        builder.ToTable("receiving_orders");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.SupplierId).HasColumnName("supplier_id");
        builder.Property(e => e.DocumentNumber).HasColumnName("document_number").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.BusinessDate).HasColumnName("business_date").IsRequired();
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
        builder.Property(e => e.VerifiedBy).HasColumnName("verified_by");
        builder.Property(e => e.VerifiedAt).HasColumnName("verified_at");
        builder.Property(e => e.ReversedBy).HasColumnName("reversed_by");
        builder.Property(e => e.ReversedAt).HasColumnName("reversed_at");
        builder.Property(e => e.ReversalReason).HasColumnName("reversal_reason");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.CompanyId, e.DocumentNumber }).IsUnique().HasDatabaseName("uq_rec_company_doc");
        builder.HasIndex(e => new { e.CompanyId, e.WarehouseId, e.Status, e.BusinessDate })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_rec_wh_status");
        builder.HasIndex(e => new { e.CompanyId, e.CreatedAt, e.Id }).IsDescending(false, true, true).HasDatabaseName("ix_receiving_orders_tenant_keyset");

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Supplier>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.SupplierId })
            .HasPrincipalKey(s => new { s.CompanyId, s.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.CreatedBy })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_rec_created_by");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.VerifiedBy })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_rec_verified_by")
            .IsRequired(false);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ReversedBy })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_rec_reversed_by")
            .IsRequired(false);
    }
}
