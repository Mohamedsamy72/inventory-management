using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(e => new { e.UserId, e.RoleId });
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.RoleId).HasColumnName("role_id");

        // ADR-014: exactly one product role per user, enforced at the database, not just in code.
        builder.HasIndex(e => e.UserId).IsUnique().HasDatabaseName("uq_user_single_role");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Role>().WithMany().HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}
