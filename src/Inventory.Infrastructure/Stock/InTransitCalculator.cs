using Inventory.Application.Common;
using Inventory.Domain.Enums;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Stock;

/// <summary>See <see cref="IInTransitCalculator"/>.</summary>
public sealed class InTransitCalculator : IInTransitCalculator
{
    private readonly InventoryDbContext _context;

    public InTransitCalculator(InventoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<decimal> GetInTransitQuantityAsync(Guid warehouseId, Guid itemId, CancellationToken cancellationToken)
    {
        decimal? total = await DispatchedQuery(warehouseId)
            .Where(si => si.ItemId == itemId)
            .SumAsync(si => (decimal?)si.DispatchedBaseQuantity, cancellationToken);

        return total ?? 0m;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetInTransitQuantitiesByItemAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        var grouped = await DispatchedQuery(warehouseId)
            .GroupBy(si => si.ItemId)
            .Select(g => new { ItemId = g.Key, Total = g.Sum(si => si.DispatchedBaseQuantity) })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(x => x.ItemId, x => x.Total);
    }

    /// <summary>ADR-018: attributed to the warehouse, never to a restaurant - joins through the
    /// parent <c>Supply</c> for its <c>WarehouseId</c> and <c>Dispatched</c> status, since
    /// neither lives on <c>SupplyItem</c> itself.</summary>
    private IQueryable<Domain.Entities.SupplyItem> DispatchedQuery(Guid warehouseId) =>
        from si in _context.SupplyItems
        join s in _context.Supplies on si.SupplyId equals s.Id
        where s.WarehouseId == warehouseId && s.Status == SupplyStatus.Dispatched
        select si;
}
