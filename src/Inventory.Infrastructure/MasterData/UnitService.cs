using Inventory.Application.Common;
using Inventory.Application.MasterData;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="IUnitService"/>.</summary>
public sealed class UnitService : IUnitService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;

    public UnitService(InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<MasterDataResult<UnitSummary>> CreateAsync(CreateUnitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var unit = new Unit(_currentUserService.CompanyId, command.NameArabic, command.Abbreviation);
        _context.Units.Add(unit);

        _auditLogger.Record(new AuditEntry(
            "UNIT_CREATED", nameof(Unit), unit.Id,
            $"تم إنشاء وحدة قياس جديدة: {unit.NameArabic}",
            OldValues: null, NewValues: new { unit.NameArabic, unit.Abbreviation }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<UnitSummary>(error)
            : MasterDataResult.Success(ToSummary(unit));
    }

    public async Task<UnitSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Unit? unit = await _context.Units.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return unit is null ? null : ToSummary(unit);
    }

    public async Task<KeysetPage<UnitSummary>> ListAsync(int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<Unit> query = _context.Units.AsNoTracking()
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<Unit> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<Unit> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<UnitSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    public async Task<MasterDataResult<UnitSummary>> UpdateAsync(Guid id, UpdateUnitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Unit? unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (unit is null)
        {
            return MasterDataResult.Failure<UnitSummary>(MasterDataError.NotFound);
        }

        var old = new { unit.NameArabic, unit.Abbreviation };
        unit.Update(command.NameArabic, command.Abbreviation);

        _auditLogger.Record(new AuditEntry(
            "UNIT_UPDATED", nameof(Unit), unit.Id,
            $"تم تعديل وحدة القياس: {old.NameArabic} → {unit.NameArabic}",
            OldValues: old, NewValues: new { unit.NameArabic, unit.Abbreviation }, Domain.Enums.AuditResult.Success));

        MasterDataError? conflict = await TrySaveAsync(cancellationToken);
        return conflict is { } error
            ? MasterDataResult.Failure<UnitSummary>(error)
            : MasterDataResult.Success(ToSummary(unit));
    }

    public async Task<MasterDataResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Unit? entity = await _context.Units.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return await MasterDataDeletion.DeleteAsync(
            _context, _auditLogger, entity, id, "UNIT_DELETED", nameof(Unit),
            $"تم حذف الوحدة: {entity?.NameArabic}", cancellationToken);
    }

    public Task<MasterDataResult<UnitSummary>> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<MasterDataResult<UnitSummary>> ReactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<MasterDataResult<UnitSummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        Unit? unit = await _context.Units.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (unit is null)
        {
            return MasterDataResult.Failure<UnitSummary>(MasterDataError.NotFound);
        }

        if (active)
        {
            unit.Reactivate();
        }
        else
        {
            unit.Deactivate();
        }

        _auditLogger.Record(new AuditEntry(
            active ? "UNIT_REACTIVATED" : "UNIT_DEACTIVATED", nameof(Unit), unit.Id,
            active ? $"تم تفعيل وحدة القياس: {unit.NameArabic}" : $"تم تعطيل وحدة القياس: {unit.NameArabic}",
            OldValues: null, NewValues: new { unit.IsActive }, Domain.Enums.AuditResult.Success));

        await _context.SaveChangesAsync(cancellationToken);
        return MasterDataResult.Success(ToSummary(unit));
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

    private static UnitSummary ToSummary(Unit unit) =>
        new(unit.Id, unit.NameArabic, unit.Abbreviation, unit.IsActive, unit.CreatedAt);
}
