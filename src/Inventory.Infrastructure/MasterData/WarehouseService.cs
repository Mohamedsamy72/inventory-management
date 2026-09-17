using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="IWarehouseService"/>.</summary>
public sealed class WarehouseService : IWarehouseService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public WarehouseService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<MasterDataResult<WarehouseSummary>> CreateAsync(CreateWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var warehouse = new Warehouse(_currentUserService.CompanyId, command.NameArabic, command.Code, command.Address, command.Description);
        _context.Warehouses.Add(warehouse);

        _auditLogger.Record(new AuditEntry(
            "WAREHOUSE_CREATED", nameof(Warehouse), warehouse.Id,
            $"تم إنشاء مستودع جديد: {warehouse.NameArabic} ({warehouse.Code})",
            OldValues: null, NewValues: new { warehouse.NameArabic, warehouse.Code }, Domain.Enums.AuditResult.Success,
            WarehouseId: warehouse.Id));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<WarehouseSummary>(error)
            : MasterDataResult.Success(ToSummary(warehouse));
    }

    public async Task<WarehouseSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await _context.Warehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        return warehouse is null ? null : ToSummary(warehouse);
    }

    public async Task<KeysetPage<WarehouseSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Warehouse> query = _context.Warehouses.AsNoTracking()
            .OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Warehouse> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Warehouse> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<WarehouseSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<MasterDataResult<WarehouseSummary>> UpdateAsync(Guid id, UpdateWarehouseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Warehouse? warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse is null)
        {
            return MasterDataResult.Failure<WarehouseSummary>(MasterDataError.NotFound);
        }

        string oldName = warehouse.NameArabic;
        warehouse.Update(command.NameArabic, command.Address, command.Description);

        _auditLogger.Record(new AuditEntry(
            "WAREHOUSE_UPDATED", nameof(Warehouse), warehouse.Id,
            $"تم تعديل المستودع: {oldName} → {warehouse.NameArabic}",
            OldValues: new { NameArabic = oldName }, NewValues: new { warehouse.NameArabic }, Domain.Enums.AuditResult.Success,
            WarehouseId: warehouse.Id));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<WarehouseSummary>(error)
            : MasterDataResult.Success(ToSummary(warehouse));
    }

    public Task<MasterDataResult<WarehouseSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<MasterDataResult<WarehouseSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<MasterDataResult<WarehouseSummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        Warehouse? warehouse = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (warehouse is null)
        {
            return MasterDataResult.Failure<WarehouseSummary>(MasterDataError.NotFound);
        }

        if (active)
        {
            warehouse.Reactivate();
        }
        else
        {
            warehouse.Deactivate();
        }

        _auditLogger.Record(new AuditEntry(
            active ? "WAREHOUSE_REACTIVATED" : "WAREHOUSE_DEACTIVATED", nameof(Warehouse), warehouse.Id,
            active ? $"تم تفعيل المستودع: {warehouse.NameArabic}" : $"تم تعطيل المستودع: {warehouse.NameArabic}",
            OldValues: null, NewValues: new { IsActive = active }, Domain.Enums.AuditResult.Success,
            WarehouseId: warehouse.Id));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(warehouse));
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

    private static WarehouseSummary ToSummary(Warehouse warehouse) => new(
        warehouse.Id, warehouse.NameArabic, warehouse.Code,
        warehouse.Status == Domain.Enums.WarehouseStatus.Active, warehouse.Address, warehouse.Description, warehouse.CreatedAt);
}
