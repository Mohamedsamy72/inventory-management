using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class CompanySettingsConfiguration : IEntityTypeConfiguration<CompanySettings>
{
    public void Configure(EntityTypeBuilder<CompanySettings> builder)
    {
        builder.ToTable("company_settings");
        builder.HasKey(e => e.CompanyId);

        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.TimezoneId).HasColumnName("timezone_id").HasMaxLength(64).IsRequired();
        builder.Property(e => e.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne<Company>().WithOne().HasForeignKey<CompanySettings>(e => e.CompanyId).OnDelete(DeleteBehavior.Cascade);
    }
}
