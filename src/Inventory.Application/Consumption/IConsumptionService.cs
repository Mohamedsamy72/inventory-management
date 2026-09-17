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

    Task<KeysetPage<ConsumptionRecordSummary>> ListAsync(Guid? restaurantId, int limit, string? cursor, CancellationToken cancellationToken);
}
