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
    /// by the application layer, which has the ledger visibility this entity does not. Also
    /// part of the <c>uq_items_base_unit (company_id, id, base_unit_id)</c> EF alternate key
    /// backing <c>item_unit_conversions</c>' composite FK - EF Core's change tracker refuses to
    /// modify any property in a tracked key regardless of whether a dependent row currently
    /// exists, so an application-layer change goes through a raw SQL <c>UPDATE</c> instead of
    /// the normal tracked-entity/<c>SaveChangesAsync</c> path (see
    /// <c>Inventory.Infrastructure.MasterData.ItemService.ChangeBaseUnitAsync</c>) - there is
    /// deliberately no domain method here that would suggest the normal path works.</summary>
    public Guid BaseUnitId { get; private set; }

    public Guid? PurchaseUnitId { get; private set; }
    public Guid? DefaultSupplierId { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string nameArabic, Guid categoryId, Guid? purchaseUnitId, Guid? defaultSupplierId, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameArabic);
        NameArabic = nameArabic;
        NameNormalized = ArabicTextNormalizer.Normalize(nameArabic);
        CategoryId = categoryId;
        PurchaseUnitId = purchaseUnitId;
        DefaultSupplierId = defaultSupplierId;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

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
