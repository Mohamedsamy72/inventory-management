using Inventory.Domain.Common;

namespace Inventory.Domain.Entities;

public sealed class Item : Entity, ITenantScopedEntity
{
    private Item()
    {
    }

    public Item(
        Guid companyId,
        string generatedCode,
        string nameArabic,
        Guid categoryId,
        Guid baseUnitId,
        Guid? purchaseUnitId,
        Guid? defaultSupplierId,
        string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generatedCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);

        Id = Guid.NewGuid();
        CompanyId = companyId;
        GeneratedCode = generatedCode;
        NameArabic = nameArabic;
        NameNormalized = ArabicTextNormalizer.Normalize(nameArabic);
        CategoryId = categoryId;
        BaseUnitId = baseUnitId;
        PurchaseUnitId = purchaseUnitId;
        DefaultSupplierId = defaultSupplierId;
        Description = description;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid CompanyId { get; private set; }

    /// <summary>Server-generated (ADR-025), gap-free, assigned atomically at creation by the
    /// document-sequence service (Phase 5) and immutable for the item's lifetime - there is no
    /// setter beyond the constructor, and never a client-supplied value.</summary>
    public string GeneratedCode { get; private set; } = string.Empty;

    public string NameArabic { get; private set; } = string.Empty;

    /// <summary>The Arabic search/uniqueness key (docs/31 section 4.2) - never displayed,
    /// never typed by a user, always derived from <see cref="NameArabic"/>.</summary>
    public string NameNormalized { get; private set; } = string.Empty;

    public Guid CategoryId { get; private set; }

    /// <summary>Immutable once any stock_ledger row exists for this item (ADR-023) - enforced
    /// by the application layer, which has the ledger visibility this entity does not.</summary>
    public Guid BaseUnitId { get; private set; }

    public Guid? PurchaseUnitId { get; private set; }
    public Guid? DefaultSupplierId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
