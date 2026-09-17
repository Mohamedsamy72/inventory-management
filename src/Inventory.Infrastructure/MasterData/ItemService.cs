using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="IItemService"/>.</summary>
public sealed class ItemService : IItemService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IDocumentSequenceService _sequenceService;

    public ItemService(
        InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger, IDocumentSequenceService sequenceService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _sequenceService = sequenceService ?? throw new ArgumentNullException(nameof(sequenceService));
    }

    public async Task<MasterDataResult<ItemSummary>> CreateAsync(CreateItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!await _context.Categories.AnyAsync(c => c.Id == command.CategoryId, cancellationToken)
            || !await _context.Units.AnyAsync(u => u.Id == command.BaseUnitId, cancellationToken))
        {
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.NotFound);
        }

        // Task 5.3/5.1 (ADR-025): the sequence allocation is a raw-SQL statement that commits
        // immediately unless an explicit transaction is already open - opened here so the
        // allocated code and the item row either both persist or neither does.
        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        string generatedCode;
        try
        {
            generatedCode = await _sequenceService.AllocateAsync(DocumentType.Item, cancellationToken);
        }
        catch (SequenceExhaustedException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.SequenceExhausted);
        }

        var item = new Item(
            _currentUserService.CompanyId, generatedCode, command.NameArabic, command.CategoryId, command.BaseUnitId,
            command.PurchaseUnitId, command.DefaultSupplierId, command.Description);
        _context.Items.Add(item);

        _auditLogger.Record(new AuditEntry(
            "ITEM_CREATED", nameof(Item), item.Id,
            $"تم إنشاء صنف جديد: {item.NameArabic} ({item.GeneratedCode})",
            OldValues: null, NewValues: new { item.NameArabic, item.GeneratedCode }, Domain.Enums.AuditResult.Success));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.DuplicateItemName);
        }

        await transaction.CommitAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(item));
    }

    public async Task<ItemSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Item? item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    public async Task<KeysetPage<ItemSummary>> ListAsync(int limit, string? cursor, string? searchQuery, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Item> query = _context.Items.AsNoTracking();

        // Task 5.14 (docs/31 §4.2, docs/29 §5.4's pg_trgm GIN index on name_normalized): the
        // same normalization the item's own name went through at creation, so a search for
        // "محمص" matches an item stored as "مُحمَّص" - diacritics/tatweel/alef-form differences
        // are exactly what NameNormalized exists to erase on both sides of the comparison.
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string normalizedQuery = ArabicTextNormalizer.Normalize(searchQuery);
            query = query.Where(i => EF.Functions.ILike(i.NameNormalized, $"%{normalizedQuery}%"));
        }

        query = query.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Item> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Item> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<ItemSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<MasterDataResult<ItemSummary>> UpdateAsync(Guid id, UpdateItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Item? item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.NotFound);
        }

        if (!await _context.Categories.AnyAsync(c => c.Id == command.CategoryId, cancellationToken))
        {
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.NotFound);
        }

        string oldName = item.NameArabic;
        item.Update(command.NameArabic, command.CategoryId, command.PurchaseUnitId, command.DefaultSupplierId, command.Description);

        _auditLogger.Record(new AuditEntry(
            "ITEM_UPDATED", nameof(Item), item.Id,
            $"تم تعديل الصنف: {oldName} → {item.NameArabic}",
            OldValues: new { NameArabic = oldName }, NewValues: new { item.NameArabic }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<ItemSummary>(error)
            : MasterDataResult.Success(ToSummary(item));
    }

    /// <summary>
    /// Updates <c>base_unit_id</c> via raw SQL rather than the tracked entity's normal
    /// <c>SaveChangesAsync</c> path - <c>ItemConfiguration</c> declares
    /// <c>(company_id, id, base_unit_id)</c> an EF alternate key (backing
    /// <c>item_unit_conversions</c>' composite FK, ADR-023), and EF Core's change tracker
    /// unconditionally refuses to modify ANY property that participates in a key it tracks, keys
    /// it did not itself allocate, regardless of whether anything currently references this
    /// specific value. An explicit transaction keeps the raw UPDATE and the audit row atomic,
    /// the same reason <see cref="IDocumentSequenceService"/> needs one.
    /// </summary>
    public async Task<MasterDataResult<ItemSummary>> ChangeBaseUnitAsync(Guid id, Guid newBaseUnitId, CancellationToken cancellationToken)
    {
        Item? item = await _context.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.NotFound);
        }

        // ADR-023, task 5.11: stock_ledger does not exist as posted data before Phase 7, so this
        // check is always false today - written correctly now so it takes effect the moment
        // Phase 7's posting service starts appending rows, without needing revisiting.
        bool hasLedgerActivity = await _context.StockLedgerEntries.AnyAsync(l => l.ItemId == id, cancellationToken);
        if (hasLedgerActivity)
        {
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.BaseUnitImmutable);
        }

        Guid oldBaseUnitId = item.BaseUnitId;

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE items SET base_unit_id = {newBaseUnitId}, updated_at = now() WHERE id = {id}", cancellationToken);

        _auditLogger.Record(new AuditEntry(
            "ITEM_BASE_UNIT_CHANGED", nameof(Item), item.Id,
            $"تم تغيير وحدة القياس الأساسية للصنف: {item.NameArabic}",
            OldValues: new { BaseUnitId = oldBaseUnitId }, NewValues: new { BaseUnitId = newBaseUnitId }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MasterDataResult.Success(ToSummary(item) with { BaseUnitId = newBaseUnitId });
    }

    public Task<MasterDataResult<ItemSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<MasterDataResult<ItemSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<MasterDataResult<ItemSummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        Item? item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return MasterDataResult.Failure<ItemSummary>(MasterDataError.NotFound);
        }

        if (active)
        {
            item.Reactivate();
        }
        else
        {
            item.Deactivate();
        }

        _auditLogger.Record(new AuditEntry(
            active ? "ITEM_REACTIVATED" : "ITEM_DEACTIVATED", nameof(Item), item.Id,
            active ? $"تم تفعيل الصنف: {item.NameArabic}" : $"تم تعطيل الصنف: {item.NameArabic}",
            OldValues: null, NewValues: new { IsActive = active }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(item));
    }

    private async Task<MasterDataError?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return MasterDataError.DuplicateItemName;
        }
    }

    private static ItemSummary ToSummary(Item item) => new(
        item.Id, item.GeneratedCode, item.NameArabic, item.CategoryId, item.BaseUnitId,
        item.PurchaseUnitId, item.DefaultSupplierId, item.Description, item.IsActive, item.CreatedAt);
}
