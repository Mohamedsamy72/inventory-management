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

    private const int ReportRowCap = 5000;
    private const string DefaultTimezoneId = "Africa/Cairo";
    private const string Last24Hours = "Last24Hours";

    public async Task<KeysetPage<ConsumptionRecordSummary>> ListAsync(
        Guid? restaurantId, DateOnly? fromDate, DateOnly? toDate, int limit, string? cursor, CancellationToken cancellationToken)
    {
        KeysetPagination.Cursor? decoded = KeysetPagination.DecodeCursor(cursor);
        TimeZoneInfo timezone = await GetCompanyTimeZoneAsync(cancellationToken);
        ConsumptionPeriod period = ResolvePeriod(fromDate, toDate, timezone);

        IQueryable<ConsumptionRecord> query = Filtered(restaurantId, period).OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        if (decoded is { } c)
        {
            query = query.Where(x => x.CreatedAt < c.CreatedAt || (x.CreatedAt == c.CreatedAt && x.Id < c.Id));
        }

        List<ConsumptionRecord> overFetched = await query.Take(limit + 1).ToListAsync(cancellationToken);
        KeysetPage<ConsumptionRecord> page = KeysetPagination.BuildPage(overFetched, limit, x => x.CreatedAt, x => x.Id);

        IReadOnlyList<ConsumptionRecordSummary> rows = await EnrichAsync(page.Items, timezone, cancellationToken);
        return new KeysetPage<ConsumptionRecordSummary>(rows, page.NextCursor);
    }

    public async Task<ConsumptionReport> GetReportAsync(Guid? restaurantId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        TimeZoneInfo timezone = await GetCompanyTimeZoneAsync(cancellationToken);
        ConsumptionPeriod period = ResolvePeriod(fromDate, toDate, timezone);
        IQueryable<ConsumptionRecord> query = Filtered(restaurantId, period);

        int totalCount = await query.CountAsync(cancellationToken);
        List<ConsumptionRecord> records = await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(ReportRowCap).ToListAsync(cancellationToken);

        // Totals are always over the WHOLE filtered set, computed in the database - never derived from the capped rows.
        var grouped = await query.GroupBy(r => new { r.ItemId, r.UnitId })
            .Select(g => new { g.Key.ItemId, g.Key.UnitId, Quantity = g.Sum(x => x.Quantity), Count = g.Count() })
            .ToListAsync(cancellationToken);
        List<Guid> groupedItemIds = grouped.Select(g => g.ItemId).Distinct().ToList();
        List<Guid> groupedUnitIds = grouped.Select(g => g.UnitId).Distinct().ToList();
        Dictionary<Guid, string> itemNames = await NamesAsync(_context.Items.AsNoTracking().Where(i => groupedItemIds.Contains(i.Id)).Select(i => new KeyValuePair<Guid, string>(i.Id, i.NameArabic)), cancellationToken);
        Dictionary<Guid, string> unitNames = await NamesAsync(_context.Units.AsNoTracking().Where(u => groupedUnitIds.Contains(u.Id)).Select(u => new KeyValuePair<Guid, string>(u.Id, u.NameArabic)), cancellationToken);
        List<ConsumptionTotal> totals = grouped
            .Select(g => new ConsumptionTotal(g.ItemId, itemNames.GetValueOrDefault(g.ItemId, "-"), g.UnitId, unitNames.GetValueOrDefault(g.UnitId, "-"), g.Quantity, g.Count))
            .OrderBy(t => t.ItemName, StringComparer.CurrentCulture).ThenBy(t => t.UnitName, StringComparer.CurrentCulture).ToList();

        Guid companyId = _currentUserService.CompanyId;
        string companyName = await _context.Companies.AsNoTracking().Where(c => c.Id == companyId)
            .Select(c => c.Name).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        string? restaurantName = restaurantId is { } rid
            ? await _context.Restaurants.AsNoTracking().Where(r => r.Id == rid).Select(r => r.NameArabic).FirstOrDefaultAsync(cancellationToken)
            : null;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new ConsumptionReport(
            period, now, FormatLocal(now, timezone), companyName, restaurantId, restaurantName,
            await EnrichAsync(records, timezone, cancellationToken), totals, totalCount, Truncated: totalCount > ReportRowCap);
    }

    /// <summary>The reporting window. Last24Hours is a rolling window ending now. Dates are calendar days in the
    /// company timezone: from 00:00 of the first date up to (excluding) 00:00 of the day after the last date, converted
    /// to UTC, so both ends are inclusive as dates and no off-by-one-day error is possible around midnight or a DST change.</summary>
    internal static ConsumptionPeriod ResolvePeriod(DateOnly? from, DateOnly? to, TimeZoneInfo timezone)
    {
        if (from is null || to is null)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return new ConsumptionPeriod(Last24Hours, null, null, now.AddHours(-24), now, timezone.Id);
        }

        return new ConsumptionPeriod("Dates", from, to, StartOfDayUtc(from.Value, timezone), StartOfDayUtc(to.Value.AddDays(1), timezone), timezone.Id);
    }

    private static DateTimeOffset StartOfDayUtc(DateOnly date, TimeZoneInfo timezone)
    {
        DateTime local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // A zone whose DST change happens AT midnight (Cairo) has no 00:00 on the spring-forward day: the day starts at 01:00.
        if (timezone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timezone), TimeSpan.Zero);
    }

    private IQueryable<ConsumptionRecord> Filtered(Guid? restaurantId, ConsumptionPeriod period)
    {
        // The tenant query filter scopes to the caller's company; the restaurant id (already scope-checked by the endpoint)
        // only narrows further. created_at is covered by ix_consumption_records_tenant_keyset (company_id, created_at, id).
        IQueryable<ConsumptionRecord> query = _context.ConsumptionRecords.AsNoTracking();
        if (restaurantId is { } id)
        {
            query = query.Where(r => r.RestaurantId == id);
        }

        DateTimeOffset fromUtc = period.FromUtc;
        DateTimeOffset toUtc = period.ToUtc;
        return period.Kind == Last24Hours
            ? query.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt <= toUtc)
            : query.Where(r => r.CreatedAt >= fromUtc && r.CreatedAt < toUtc);
    }

    private async Task<TimeZoneInfo> GetCompanyTimeZoneAsync(CancellationToken cancellationToken)
    {
        // company_settings carries no tenant query filter, so the caller's company is stated explicitly.
        Guid companyId = _currentUserService.CompanyId;
        string? id = await _context.CompanySettings.AsNoTracking().Where(s => s.CompanyId == companyId).Select(s => s.TimezoneId).FirstOrDefaultAsync(cancellationToken);
        return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(id) ? DefaultTimezoneId : id);
    }

    private static string FormatLocal(DateTimeOffset value, TimeZoneInfo timezone) =>
        TimeZoneInfo.ConvertTime(value, timezone).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<Dictionary<Guid, string>> NamesAsync(IQueryable<KeyValuePair<Guid, string>> query, CancellationToken cancellationToken) =>
        (await query.ToListAsync(cancellationToken)).ToDictionary(p => p.Key, p => p.Value);

    private async Task<IReadOnlyList<ConsumptionRecordSummary>> EnrichAsync(IReadOnlyList<ConsumptionRecord> records, TimeZoneInfo timezone, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        List<Guid> itemIds = records.Select(r => r.ItemId).Distinct().ToList();
        List<Guid> unitIds = records.Select(r => r.UnitId).Distinct().ToList();
        List<Guid> restaurantIds = records.Select(r => r.RestaurantId).Distinct().ToList();
        List<Guid> userIds = records.Select(r => r.RecordedBy).Distinct().ToList();

        Dictionary<Guid, string> items = await NamesAsync(_context.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).Select(i => new KeyValuePair<Guid, string>(i.Id, i.NameArabic)), cancellationToken);
        Dictionary<Guid, string> units = await NamesAsync(_context.Units.AsNoTracking().Where(u => unitIds.Contains(u.Id)).Select(u => new KeyValuePair<Guid, string>(u.Id, u.NameArabic)), cancellationToken);
        Dictionary<Guid, string> restaurants = await NamesAsync(_context.Restaurants.AsNoTracking().Where(r => restaurantIds.Contains(r.Id)).Select(r => new KeyValuePair<Guid, string>(r.Id, r.NameArabic)), cancellationToken);
        Dictionary<Guid, string> users = await NamesAsync(_context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => new KeyValuePair<Guid, string>(u.Id, u.FullName)), cancellationToken);

        return records.Select(r => ToSummary(r) with
        {
            ItemName = items.GetValueOrDefault(r.ItemId),
            UnitName = units.GetValueOrDefault(r.UnitId),
            RestaurantName = restaurants.GetValueOrDefault(r.RestaurantId),
            RecordedByName = users.GetValueOrDefault(r.RecordedBy),
            RecordedAtLocal = FormatLocal(r.CreatedAt, timezone),
        }).ToList();
    }

    private static ConsumptionRecordSummary ToSummary(ConsumptionRecord record) => new(
        record.Id, record.RestaurantId, record.ItemId, record.Quantity, record.UnitId, record.ConsumptionDate,
        record.RecordedBy, record.Notes, record.CreatedAt);
}
