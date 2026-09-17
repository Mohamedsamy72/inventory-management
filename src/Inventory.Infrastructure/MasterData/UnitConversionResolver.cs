using Inventory.Application.Common;
using Inventory.Domain.Entities;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.MasterData;

/// <summary>See <see cref="IUnitConversionResolver"/>.</summary>
public sealed class UnitConversionResolver : IUnitConversionResolver
{
    private readonly InventoryDbContext _context;

    public UnitConversionResolver(InventoryDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<UnitConversionResolution> ResolveAsync(Guid itemId, Guid unitId, decimal quantity, CancellationToken cancellationToken)
    {
        Item item = await _context.Items.AsNoTracking().FirstAsync(i => i.Id == itemId, cancellationToken);

        if (unitId == item.BaseUnitId)
        {
            return new UnitConversionResolution(true, quantity);
        }

        ItemUnitConversion? conversion = await _context.ItemUnitConversions.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ItemId == itemId && c.FromUnitId == unitId && c.IsActive, cancellationToken);

        if (conversion is null)
        {
            return new UnitConversionResolution(false, 0m);
        }

        return new UnitConversionResolution(true, quantity * conversion.ConversionFactor);
    }
}
