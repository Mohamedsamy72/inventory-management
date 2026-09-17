using Inventory.Application.Common;
using Inventory.Domain.Enums;

namespace Inventory.Application.Discrepancies;

/// <summary>Phase 12 - a single lifecycle (`Open -> Investigating -> Resolved`) for every
/// variance the system produces (docs/09 tasks 12.1-12.7). `Discrepancy` itself was already
/// built in Phase 2; this is the read/resolve surface over it.</summary>
public interface IDiscrepancyService
{
    Task<DiscrepancySummary?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Task 12.3's scoping is applied here, not in the endpoint: <paramref name="warehouseIds"/>
    /// and <paramref name="restaurantIds"/> are null for an unrestricted caller (Owner/Admin) and
    /// a concrete (possibly empty) set for a scoped one - a discrepancy matches if EITHER its
    /// `WarehouseId` is in the warehouse set OR its `RestaurantId` is in the restaurant set,
    /// whichever dimension the caller supplies. <paramref name="type"/> further restricts to one
    /// discrepancy type (Restaurant Supervisor sees only `SupplyReceiptVariance`, never
    /// `ReceivingVariance`/`StockCountVariance`, which are warehouse-only concepts).</summary>
    Task<KeysetPage<DiscrepancySummary>> ListAsync(
        int limit, string? cursor, IReadOnlySet<Guid>? warehouseIds, IReadOnlySet<Guid>? restaurantIds, DiscrepancyType? type, CancellationToken cancellationToken);

    /// <summary>Task 12.4/12.5: requires a non-empty Arabic reason, is audited, and NEVER posts a
    /// stock movement - reconciliation of stock happens only through a stock count (ADR-021,
    /// Phase 13). There is no path from this method to <c>IStockPostingService</c>.</summary>
    Task<TransactionalResult<DiscrepancySummary>> ResolveAsync(Guid id, ResolveDiscrepancyCommand command, CancellationToken cancellationToken);
}
