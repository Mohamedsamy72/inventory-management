using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockLedgerEntryConfiguration : IEntityTypeConfiguration<StockLedgerEntry>
{
    // A ValueConverter's expression trees cannot contain a switch or throw expression, so the
    // mapping is a plain dictionary indexer call instead - functionally identical to a switch,
    // just spelled in a form the expression tree compiler accepts.
    private static readonly Dictionary<MovementType, string> MovementTypeToDb = new()
    {
        [MovementType.OpeningBalance] = "OPENING_BALANCE",
        [MovementType.IncomingPosted] = "INCOMING_POSTED",
        [MovementType.IncomingReconciliation] = "INCOMING_RECONCILIATION",
        [MovementType.RestaurantReceiptConfirmed] = "RESTAURANT_RECEIPT_CONFIRMED",
        [MovementType.PhysicalAdjustment] = "PHYSICAL_ADJUSTMENT",
    };

    private static readonly Dictionary<string, MovementType> MovementTypeFromDb =
        MovementTypeToDb.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>
    /// Persists the exact UPPER_SNAKE_CASE literals docs/02 section 4.2 names as the allowed
    /// set - "and nothing else" - rather than the C# enum member's PascalCase spelling.
    /// </summary>
    private static readonly ValueConverter<MovementType, string> MovementTypeConverter = new(
        toDb => MovementTypeToDb[toDb],
        fromDb => MovementTypeFromDb[fromDb]);

    public void Configure(EntityTypeBuilder<StockLedgerEntry> builder)
    {
        builder.ToTable("stock_ledger");
        builder.HasKey(e => e.Id);
        builder.HasTenantAlternateKey();

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.WarehouseId).HasColumnName("warehouse_id");
        builder.Property(e => e.ItemId).HasColumnName("item_id");
        builder.Property(e => e.MovementType).HasColumnName("movement_type").HasConversion(MovementTypeConverter).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.BaseQuantity).HasColumnName("base_quantity").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.ReferenceType).HasColumnName("reference_type").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.ReferenceId).HasColumnName("reference_id").IsRequired();
        builder.Property(e => e.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(18,4)");
        builder.Property(e => e.TotalCost).HasColumnName("total_cost").HasColumnType("numeric(18,4)");
        builder.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        // docs/29 section 4.3 / BR-29-5: a document posts a given movement for a given item once.
        builder.HasIndex(e => new { e.CompanyId, e.ReferenceType, e.ReferenceId, e.ItemId, e.MovementType })
            .IsUnique().HasDatabaseName("uq_ledger_posting");

        builder.HasIndex(e => new { e.CompanyId, e.WarehouseId, e.ItemId, e.OccurredAt })
            .IsDescending(false, false, false, true).HasDatabaseName("ix_ledger_wh_item_time");
        builder.HasIndex(e => new { e.CompanyId, e.ReferenceType, e.ReferenceId }).HasDatabaseName("ix_ledger_reference");
        builder.HasIndex(e => new { e.CompanyId, e.MovementType, e.OccurredAt })
            .IsDescending(false, false, true).HasDatabaseName("ix_ledger_type_time");

        builder.HasOne<Warehouse>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.WarehouseId })
            .HasPrincipalKey(w => new { w.CompanyId, w.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Item>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ItemId })
            .HasPrincipalKey(i => new { i.CompanyId, i.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.UnitId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ledger_unit");

        builder.HasOne<User>().WithMany()
            .HasForeignKey(e => new { e.CompanyId, e.ActorUserId })
            .HasPrincipalKey(u => new { u.CompanyId, u.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ledger_actor");
    }
}
