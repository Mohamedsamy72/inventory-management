using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        builder.Property(e => e.Endpoint).HasColumnName("endpoint").HasMaxLength(200).IsRequired();
        builder.Property(e => e.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.ResponseStatusCode).HasColumnName("response_status_code").IsRequired();
        builder.Property(e => e.ResponsePayloadJson).HasColumnName("response_payload").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at").IsRequired();

        // docs/29 section 5.5 also lists a plain (non-unique) ix_idem_lookup index on these
        // exact columns - EF collapses a second index over identical columns into the first
        // (see SupplyItemConfiguration for the same pattern), so only the unique one is declared
        // here; it already serves every query the plain index would have.
        builder.HasIndex(e => new { e.CompanyId, e.UserId, e.IdempotencyKey }).IsUnique().HasDatabaseName("uq_idempotency_key");
        builder.HasIndex(e => e.ExpiresAt).HasDatabaseName("ix_idem_expiry");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.UserId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
