using Inventory.Application.Common;

namespace Inventory.Application.Consumption;

/// <summary>docs/04 §12 (REQ-07, docs/26) - a requirement present in the domain specification and
/// already fully migrated (Phase 2's `ConsumptionRecord`/`consumption_records`) but never
/// scheduled by name in docs/09's phase list; closed here as a Phase T1 traceability gap (docs/26
/// rule 2: "every test ID must exist as a real, named test by Phase T1", which first requires the
/// requirement to exist at all). Restaurant Supervisor logs ingredient usage for menu-yield/waste
/// analysis - the log NEVER touches warehouse stock (docs/02 §3.D); there is no path from this
/// service to `IStockPostingService`.</summary>
public interface IConsumptionService
{
    Task<MasterDataResult<ConsumptionRecordSummary>> RecordAsync(RecordConsumptionCommand command, CancellationToken cancellationToken);

    /// <summary>Server-side filtered, keyset-paged list. With no <paramref name="fromDate"/>/<paramref name="toDate"/> the window is the
    /// rolling last 24 hours; otherwise both are calendar dates in the company timezone, inclusive (from = to is a single
    /// date). The caller (endpoint) validates the pair; scope/permission are enforced before this is called.</summary>
    Task<KeysetPage<ConsumptionRecordSummary>> ListAsync(Guid? restaurantId, DateOnly? fromDate, DateOnly? toDate, int limit, string? cursor, CancellationToken cancellationToken);

    /// <summary>The same filter as <see cref="ListAsync"/>, unpaged (capped) with database-side totals - the print report.</summary>
    Task<ConsumptionReport> GetReportAsync(Guid? restaurantId, DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken);
}
