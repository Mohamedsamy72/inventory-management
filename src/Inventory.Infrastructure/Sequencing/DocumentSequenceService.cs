using System.Globalization;
using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Sequencing;

/// <summary>See <see cref="IDocumentSequenceService"/>.</summary>
public sealed class DocumentSequenceService : IDocumentSequenceService
{
    /// <summary>The `document_sequences.period_key` column is `VARCHAR(6)` (docs/29 §4.2) -
    /// too narrow for the literal string "LIFETIME" docs/28 §5.2's prose names. Truncated to
    /// exactly 6 characters, matching the value <see cref="DocumentSequence"/> itself already
    /// documented as the resolution when the entity was built in Phase 2. Recorded as a
    /// deliberate deviation in docs/27 §13.2, not silently invented here.</summary>
    private const string LifetimePeriodKey = "LIFETI";

    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DocumentSequenceService(InventoryDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    public async Task<string> AllocateAsync(DocumentType documentType, CancellationToken cancellationToken)
    {
        (string prefix, int digits, bool isMonthly) = FormatFor(documentType);
        string periodKey = isMonthly ? await MonthlyPeriodKeyAsync(cancellationToken) : LifetimePeriodKey;
        Guid companyId = _currentUserService.CompanyId;

        // ADR-025: one INSERT ... ON CONFLICT ... DO UPDATE ... RETURNING, executed against the
        // SAME context/connection the caller's own SaveChangesAsync will use - so this
        // allocation rolls back with the caller's transaction rather than burning a number on a
        // rejected document (AC-28-3). Database.SqlQuery<T> (not FromSqlInterpolated, which
        // requires a mapped entity result) is EF Core's mechanism for a raw scalar result set.
        // ToListAsync, not SingleAsync: EF tries to COMPOSE an outer query (e.g. a LIMIT
        // wrapper) around anything queried further, and an INSERT ... RETURNING statement
        // cannot be composed into an outer SELECT - materializing the raw result directly and
        // taking Single() client-side avoids that.
        List<long> results = await _context.Database
            .SqlQuery<long>(
                $"""
                INSERT INTO document_sequences (company_id, document_type, period_key, last_value, updated_at)
                VALUES ({companyId}, {documentType.ToString()}, {periodKey}, 1, now())
                ON CONFLICT (company_id, document_type, period_key)
                DO UPDATE SET last_value = document_sequences.last_value + 1,
                              updated_at = now()
                RETURNING last_value
                """)
            .ToListAsync(cancellationToken);
        long nextValue = results.Single();

        long ceiling = (long)Math.Pow(10, digits) - 1;
        if (nextValue > ceiling)
        {
            throw new SequenceExhaustedException(documentType);
        }

        string paddedValue = nextValue.ToString(new string('0', digits), CultureInfo.InvariantCulture);
        return isMonthly ? $"{prefix}-{periodKey}-{paddedValue}" : $"{prefix}-{paddedValue}";
    }

    /// <summary>docs/28 §4's per-type prefix, digit width, and whether the series is monthly
    /// (docs/28 §Overflow's two ceilings: 999999 lifetime, 9999 monthly).</summary>
    private static (string Prefix, int Digits, bool IsMonthly) FormatFor(DocumentType documentType) => documentType switch
    {
        DocumentType.Item => ("ITM", 6, false),
        DocumentType.ReceivingOrder => ("REC", 4, true),
        DocumentType.SupplyRequest => ("REQ", 4, true),
        DocumentType.Supply => ("SUP", 4, true),
        DocumentType.StockCount => ("CNT", 4, true),
        DocumentType.Discrepancy => ("DSC", 4, true),
        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type."),
    };

    /// <summary>ADR-024: derived from the business date in the tenant's configured timezone, not
    /// UTC - a document created late in the Cairo evening must land in the correct month even
    /// though UTC may already have rolled to the next day (or the previous one). Defaults to
    /// Africa/Cairo when no <see cref="CompanySettings"/> row exists yet - company/tenant
    /// provisioning has no endpoint anywhere in this plan (docs/09 never schedules one), so a
    /// missing settings row is the normal case, not an error; Africa/Cairo is the same default
    /// the entity's own constructor would have set.</summary>
    private async Task<string> MonthlyPeriodKeyAsync(CancellationToken cancellationToken)
    {
        string timezoneId = await _context.CompanySettings
            .AsNoTracking()
            .Where(s => s.CompanyId == _currentUserService.CompanyId)
            .Select(s => s.TimezoneId)
            .FirstOrDefaultAsync(cancellationToken) ?? "Africa/Cairo";

        TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        DateTimeOffset businessDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);
        return businessDate.ToString("yyyyMM", CultureInfo.InvariantCulture);
    }
}
