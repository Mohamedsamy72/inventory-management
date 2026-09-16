using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(e => new { e.RoleId, e.PermissionId });
        builder.Property(e => e.RoleId).HasColumnName("role_id");
        builder.Property(e => e.PermissionId).HasColumnName("permission_id");

        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Permission>().WithMany().HasForeignKey(e => e.PermissionId).OnDelete(DeleteBehavior.Cascade);

        // Task 2.27: the default role -> permission matrix (docs/03 section 3). Admin never
        // seeds the four non-grantable codes; Role Denial (Phase 4) is the actual enforcement,
        // this omission is just consistency.
        builder.HasData(SeedData.RolePermissions.Select(rp => new
        {
            rp.RoleId,
            rp.PermissionId,
        }));
    }
}
