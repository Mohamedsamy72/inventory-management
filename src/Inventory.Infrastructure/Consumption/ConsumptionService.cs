using Inventory.Application.Common;
using Inventory.Application.Consumption;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Consumption;

/// <summary>See <see cref="IConsumptionService"/>.</summary>
public sealed class ConsumptionService : IConsumptionService
{
    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitConversionResolver _unitConversionResolver;

    public ConsumptionService(
        InventoryDbContext context, ICurrentUserService currentUserService, IAuditLogger auditLogger, IUnitConversionResolver unitConversionResolver)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _unitConversionResolver = unitConversionResolver ?? throw new ArgumentNullException(nameof(unitConversionResolver));
    }

    public async Task<MasterDataResult<ConsumptionRecordSummary>> RecordAsync(RecordConsumptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool restaurantExists = await _context.Restaurants.AsNoTracking().AnyAsync(r => r.Id == command.RestaurantId, cancellationToken);
        if (!restaurantExists)
        {
            return MasterDataResult.Failure<ConsumptionRecordSummary>(MasterDataError.NotFound);
        }

        bool itemExists = await _context.Items.AsNoTracking().AnyAsync(i => i.Id == command.ItemId, cancellationToken);
        if (!itemExists)
        {
            return MasterDataResult.Failure<ConsumptionRecordSummary>(MasterDataError.NotFound);
        }

        UnitConversionResolution resolution = await _unitConversionResolver.ResolveAsync(command.ItemId, command.UnitId, command.Quantity, cancellationToken);
        if (!resolution.Succeeded)
        {
            return MasterDataResult.Failure<ConsumptionRecordSummary>(MasterDataError.ConversionNotDefined);
        }

        // docs/02 §3.D: statistical only - no IStockPostingService call anywhere in this method,
        // deliberately. Consumption records never touch warehouse stock.
        var record = new ConsumptionRecord(
            _currentUserService.CompanyId, command.RestaurantId, command.ItemId, command.Quantity, command.UnitId,
            resolution.BaseQuantity, command.ConsumptionDate, _currentUserService.UserId, command.Notes);
        _context.ConsumptionRecords.Add(record);

        _auditLogger.Record(new AuditEntry(
            "CONSUMPTION_RECORDED", nameof(ConsumptionRecord), record.Id,
            $"تم تسجيل استهلاك بتاريخ {record.ConsumptionDate:yyyy-MM-dd}",
            OldValues: null, NewValues: new { record.ItemId, record.Quantity, record.ConsumptionDate }, Domain.Enums.AuditResult.Success,
            RestaurantId: record.RestaurantId));

        await _context.SaveChangesAsync(cancellationToken);

        return MasterDataResult.Success(ToSummary(record));
    }

    public async Task<KeysetPage<ConsumptionRecordSummary>> ListAsync(Guid? restaurantId, int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);

        IQueryable<ConsumptionRecord> query = _context.ConsumptionRecords.AsNoTracking();

        if (restaurantId is { } id)
        {
            query = query.Where(r => r.RestaurantId == id);
        }

        query = query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<ConsumptionRecord> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<ConsumptionRecord> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        return new KeysetPage<ConsumptionRecordSummary>(page.Items.Select(ToSummary).ToList(), page.NextCursor);
    }

    private static ConsumptionRecordSummary ToSummary(ConsumptionRecord record) => new(
        record.Id, record.RestaurantId, record.ItemId, record.Quantity, record.UnitId, record.ConsumptionDate,
        record.RecordedBy, record.Notes, record.CreatedAt);
}
