using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Name).HasColumnName("name").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        // Task 2.27: the five roles, seeded so they exist from the initial migration. No
        // Accountant row (ADR-005).
        builder.HasData(SeedData.Roles.Select(role => new
        {
            role.Id,
            role.Name,
            CreatedAt = SeedData.SeedTimestamp,
        }));
    }
}
