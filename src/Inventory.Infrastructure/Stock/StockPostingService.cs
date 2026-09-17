using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Domain.Enums;
using Inventory.Domain.Services;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Stock;

/// <summary>See <see cref="IStockPostingService"/>.</summary>
public sealed class StockPostingService : IStockPostingService
{
    private static readonly HashSet<MovementType> RecomputeMovementTypes =
    [
        MovementType.OpeningBalance, MovementType.IncomingPosted, MovementType.IncomingReconciliation,
    ];

    private readonly InventoryDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICostingEngine _costingEngine;

    public StockPostingService(InventoryDbContext context, ICurrentUserService currentUserService, ICostingEngine costingEngine)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _costingEngine = costingEngine ?? throw new ArgumentNullException(nameof(costingEngine));
    }

    public async Task PostAsync(IReadOnlyList<StockPostingLine> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            return;
        }

        // Task 7.6: ascending item_id - the mandatory deadlock-avoidance rule, applied here so
        // no future caller needs to remember it.
        IReadOnlyList<StockPostingLine> ordered = lines.OrderBy(l => l.ItemId).ToList();

        Guid companyId = _currentUserService.CompanyId;
        Guid actorUserId = _currentUserService.UserId;
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

        foreach (StockPostingLine line in ordered)
        {
            bool isRecompute = RecomputeMovementTypes.Contains(line.MovementType);

            decimal unitCost;
            if (line.BaseQuantity < 0 && !isRecompute)
            {
                // docs/30 §5.1: the primary guard for a plain deduction - a single conditional
                // atomic UPDATE, evaluated under Postgres's own row lock. Executes immediately
                // (see the interface remarks on why the caller must already have a transaction
                // open).
                unitCost = await DeductAsync(companyId, line, cancellationToken);
            }
            else
            {
                unitCost = await PostViaTrackedBalanceAsync(companyId, line, isRecompute, cancellationToken);
            }

            var ledgerEntry = new StockLedgerEntry(
                companyId, line.WarehouseId, line.ItemId, line.MovementType, line.Quantity, line.BaseQuantity,
                line.UnitId, line.ReferenceType, line.ReferenceId, unitCost, line.BaseQuantity * unitCost,
                actorUserId, occurredAt);
            _context.StockLedgerEntries.Add(ledgerEntry);
        }

        // One flush for the whole batch - the tracked-entity lines' xmin tokens are checked
        // here (task 7.5); a conflict throws DbUpdateConcurrencyException for the caller to map
        // to 409 CONCURRENCY_CONFLICT. The caller's own transaction (see interface remarks)
        // still covers everything, including the raw-SQL deductions already executed above.
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>docs/30 §5.1. Returns the average cost in effect at the moment of deduction (for
    /// the ledger row) via the same statement's <c>RETURNING</c> clause, so no second round trip
    /// can observe a different value than the one the deduction itself just locked and read.</summary>
    private async Task<decimal> DeductAsync(Guid companyId, StockPostingLine line, CancellationToken cancellationToken)
    {
        decimal deduction = -line.BaseQuantity;

        List<decimal> results = await _context.Database
            .SqlQuery<decimal>(
                $"""
                UPDATE stock_balances
                   SET quantity = quantity - {deduction},
                       updated_at = now()
                 WHERE company_id = {companyId}
                   AND warehouse_id = {line.WarehouseId}
                   AND item_id = {line.ItemId}
                   AND quantity >= {deduction}
                RETURNING average_unit_cost
                """)
            .ToListAsync(cancellationToken);

        if (results.Count == 0)
        {
            throw new InsufficientStockException(line.ItemId, line.WarehouseId);
        }

        return results[0];
    }

    /// <summary>The WAC-recompute path (inbound movements, including a negative reconciliation)
    /// and the plain-adjustment path (a positive `PhysicalAdjustment`, valued at the current
    /// cost) - both go through the normal tracked-entity/<c>SaveChangesAsync</c> route, so both
    /// are protected by the <c>xmin</c> concurrency token configured on <see cref="StockBalance"/>
    /// (task 7.5) without any extra code: EF Core throws <see cref="DbUpdateConcurrencyException"/>
    /// on its own the moment a conflicting write is detected at save time.</summary>
    private async Task<decimal> PostViaTrackedBalanceAsync(Guid companyId, StockPostingLine line, bool isRecompute, CancellationToken cancellationToken)
    {
        StockBalance? balance = await _context.StockBalances.FirstOrDefaultAsync(
            b => b.CompanyId == companyId && b.WarehouseId == line.WarehouseId && b.ItemId == line.ItemId, cancellationToken);

        decimal currentQuantity = balance?.Quantity ?? 0m;
        decimal currentAverageCost = balance?.AverageUnitCost ?? 0m;

        CostingResult result = isRecompute
            ? _costingEngine.RecomputeWeightedAverage(currentQuantity, currentAverageCost, line.BaseQuantity, line.UnitCost!.Value)
            : _costingEngine.ValueAtCurrentCost(currentQuantity, currentAverageCost, line.BaseQuantity);

        if (result.NewQuantity < 0)
        {
            // ADR-021: a negative reconciliation or adjustment large enough to breach zero is
            // exactly as prohibited as an ordinary deduction exceeding the balance.
            throw new InsufficientStockException(line.ItemId, line.WarehouseId);
        }

        if (balance is null)
        {
            Item item = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == line.ItemId, cancellationToken);
            balance = new StockBalance(companyId, line.WarehouseId, line.ItemId, item.BaseUnitId);
            _context.StockBalances.Add(balance);
        }

        balance.SetQuantityAndCost(result.NewQuantity, result.NewAverageCost);
        return result.MovementUnitCost;
    }
}
