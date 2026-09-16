using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
{
    public void Configure(EntityTypeBuilder<DocumentSequence> builder)
    {
        builder.ToTable("document_sequences", t => t.HasCheckConstraint("ck_document_sequences_last_value", "last_value >= 0"));
        builder.HasKey(e => new { e.CompanyId, e.DocumentType, e.PeriodKey });

        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.DocumentType).HasColumnName("document_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(e => e.PeriodKey).HasColumnName("period_key").HasMaxLength(6).IsRequired();
        builder.Property(e => e.LastValue).HasColumnName("last_value").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Company>().WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
