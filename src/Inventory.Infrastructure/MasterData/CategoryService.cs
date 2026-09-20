using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="ICategoryService"/>.</summary>
public sealed class CategoryService : ICategoryService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public CategoryService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<MasterDataResult<CategorySummary>> CreateAsync(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = new Category(_currentUserService.CompanyId, command.NameArabic, command.Description);
        _context.Categories.Add(category);

        _auditLogger.Record(new AuditEntry(
            "CATEGORY_CREATED", nameof(Category), category.Id,
            $"تم إنشاء قسم جديد: {category.NameArabic}",
            OldValues: null, NewValues: new { category.NameArabic, category.Description }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<CategorySummary>(error)
            : MasterDataResult.Success(ToSummary(category));
    }

    public async Task<CategorySummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Category? category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return category is null ? null : ToSummary(category);
    }

    public async Task<KeysetPage<CategorySummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Category> query = _context.Categories.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Category> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Category> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<CategorySummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<MasterDataResult<CategorySummary>> UpdateAsync(Guid id, UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Category? category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return MasterDataResult.Failure<CategorySummary>(MasterDataError.NotFound);
        }

        string oldName = category.NameArabic;
        category.Rename(command.NameArabic);

        _auditLogger.Record(new AuditEntry(
            "CATEGORY_UPDATED", nameof(Category), category.Id,
            $"تم تعديل القسم: {oldName} → {category.NameArabic}",
            OldValues: new { NameArabic = oldName }, NewValues: new { category.NameArabic }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<CategorySummary>(error)
            : MasterDataResult.Success(ToSummary(category));
    }

    public async Task<MasterDataResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Category? entity = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return await MasterDataDeletion.DeleteAsync(
            _context, _auditLogger, entity, id, "CATEGORY_DELETED", nameof(Category),
            $"تم حذف القسم: {entity?.NameArabic}", cancellationToken);
    }

    public Task<MasterDataResult<CategorySummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<MasterDataResult<CategorySummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<MasterDataResult<CategorySummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        Category? category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return MasterDataResult.Failure<CategorySummary>(MasterDataError.NotFound);
        }

        if (active)
        {
            category.Reactivate();
        }
        else
        {
            category.Deactivate();
        }

        _auditLogger.Record(new AuditEntry(
            active ? "CATEGORY_REACTIVATED" : "CATEGORY_DEACTIVATED", nameof(Category), category.Id,
            active ? $"تم تفعيل القسم: {category.NameArabic}" : $"تم تعطيل القسم: {category.NameArabic}",
            OldValues: null, NewValues: new { category.IsActive }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(category));
    }

    /// <summary>Saves and translates a unique-constraint violation (`uq_categories_company_name`)
    /// into <see cref="MasterDataError.DuplicateName"/> instead of letting the raw
    /// <see cref="DbUpdateException"/> reach <c>GlobalExceptionHandler</c> as an opaque 500 -
    /// shared shape every master-data service below reuses.</summary>
    private async Task<MasterDataError?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return MasterDataError.DuplicateName;
        }
    }

    private static CategorySummary ToSummary(Category category) =>
        new(category.Id, category.NameArabic, category.Description, category.IsActive, category.CreatedAt);
}
