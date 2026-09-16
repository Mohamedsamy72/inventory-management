using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class UserRestaurantScopeConfiguration : IEntityTypeConfiguration<UserRestaurantScope>
{
    public void Configure(EntityTypeBuilder<UserRestaurantScope> builder)
    {
        builder.ToTable("user_restaurant_scopes");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.RestaurantId).HasColumnName("restaurant_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.UserId, e.RestaurantId }).IsUnique().HasDatabaseName("uq_user_restaurant");
        builder.HasIndex(e => e.UserId).HasDatabaseName("ix_urs_user");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.UserId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Restaurant>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.RestaurantId })
            .HasPrincipalKey(r => new { r.CompanyId, r.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
