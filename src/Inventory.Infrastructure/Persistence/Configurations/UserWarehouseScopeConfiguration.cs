using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class UserWarehouseScopeConfiguration : IEntityTypeConfiguration<UserWarehouseScope>
{
    public void Configure(EntityTypeBuilder<UserWarehouseScope> builder)
    {
        builder.ToTable("user_warehouse_scopes");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.UserId, e.WarehouseId }).IsUnique().HasDatabaseName("uq_user_warehouse");
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_uws_user");

        // DB-04: real composite tenant FKs (docs/06 only carried SQL comments for these).
        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.UserId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
