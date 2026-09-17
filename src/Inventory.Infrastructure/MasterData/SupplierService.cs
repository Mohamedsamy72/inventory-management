using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="ISupplierService"/>.</summary>
public sealed class SupplierService : ISupplierService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public SupplierService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<MasterDataResult<SupplierSummary>> CreateAsync(CreateSupplierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var supplier = new Supplier(_currentUserService.CompanyId, command.NameArabic, command.Phone, command.ContactPerson, command.Address, command.Notes);
        _context.Suppliers.Add(supplier);

        _auditLogger.Record(new AuditEntry(
            "SUPPLIER_CREATED", nameof(Supplier), supplier.Id,
            $"تم إنشاء مورد جديد: {supplier.NameArabic}",
            OldValues: null, NewValues: new { supplier.NameArabic, supplier.Phone }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<SupplierSummary>(error)
            : MasterDataResult.Success(ToSummary(supplier));
    }

    public async Task<SupplierSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Supplier? supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return supplier is null ? null : ToSummary(supplier);
    }

    public async Task<KeysetPage<SupplierSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Supplier> query = _context.Suppliers.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Supplier> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Supplier> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<SupplierSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<MasterDataResult<SupplierSummary>> UpdateAsync(Guid id, UpdateSupplierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Supplier? supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            return MasterDataResult.Failure<SupplierSummary>(MasterDataError.NotFound);
        }

        string oldName = supplier.NameArabic;
        supplier.Update(command.NameArabic, command.Phone, command.ContactPerson, command.Address, command.Notes);

        _auditLogger.Record(new AuditEntry(
            "SUPPLIER_UPDATED", nameof(Supplier), supplier.Id,
            $"تم تعديل المورد: {oldName} → {supplier.NameArabic}",
            OldValues: new { NameArabic = oldName }, NewValues: new { supplier.NameArabic }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<SupplierSummary>(error)
            : MasterDataResult.Success(ToSummary(supplier));
    }

    public Task<MasterDataResult<SupplierSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<MasterDataResult<SupplierSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<MasterDataResult<SupplierSummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        Supplier? supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (supplier is null)
        {
            return MasterDataResult.Failure<SupplierSummary>(MasterDataError.NotFound);
        }

        if (active)
        {
            supplier.Reactivate();
        }
        else
        {
            supplier.Deactivate();
        }

        _auditLogger.Record(new AuditEntry(
            active ? "SUPPLIER_REACTIVATED" : "SUPPLIER_DEACTIVATED", nameof(Supplier), supplier.Id,
            active ? $"تم تفعيل المورد: {supplier.NameArabic}" : $"تم تعطيل المورد: {supplier.NameArabic}",
            OldValues: null, NewValues: new { supplier.IsActive }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(supplier));
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
            return MasterDataError.DuplicateName;
        }
    }

    private static SupplierSummary ToSummary(Supplier supplier) =>
        new(supplier.Id, supplier.NameArabic, supplier.Phone, supplier.ContactPerson, supplier.Address, supplier.Notes, supplier.IsActive, supplier.CreatedAt);
}
