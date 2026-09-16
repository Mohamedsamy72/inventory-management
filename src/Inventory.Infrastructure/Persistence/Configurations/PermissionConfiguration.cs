using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(250).IsRequired();
        builder.Property(e => e.Module).HasColumnName("module").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        // IsGrantable is computed from Code, never persisted (see the entity's own remarks).
        builder.Ignore(e => e.IsGrantable);

        // Task 2.27: the permission catalogue (docs/03 section 3). costs:view, valuation:view,
        // audit:view, audit:export are included here like every other code - IsGrantable (not
        // this seed) is what makes them non-grantable (ADR-012).
        builder.HasData(SeedData.Permissions.Select(permission => new
        {
            permission.Id,
            permission.Code,
            permission.Description,
            permission.Module,
            CreatedAt = SeedData.SeedTimestamp,
        }));
    }
}
