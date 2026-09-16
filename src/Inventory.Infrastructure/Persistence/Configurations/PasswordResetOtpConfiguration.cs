using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetOtpConfiguration : IEntityTypeConfiguration<PasswordResetOtp>
{
    public void Configure(EntityTypeBuilder<PasswordResetOtp> builder)
    {
        builder.ToTable("password_reset_otps");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.OtpHash).HasColumnName("otp_hash").HasMaxLength(128).IsRequired();
        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(e => e.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.CreatedIp).HasColumnName("created_ip").HasMaxLength(45);

        builder.Ignore(e => e.IsUsable);

        builder.HasIndex(e => new { e.UserId, e.ExpiresAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_otp_user_active")
            .HasFilter("consumed_at IS NULL");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.UserId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
