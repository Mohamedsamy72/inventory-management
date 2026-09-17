namespace Inventory.Application.Common;

/// <summary>Task 6.3 - the ONLY server-side path from an entered quantity in an arbitrary unit
/// to the item's base-unit quantity. Every later phase that accepts a quantity in a non-base
/// unit (receiving, supply, stock counts) must resolve through this, never accept a
/// client-submitted base quantity directly (docs/25 "Client-Side Calculation Trust").</summary>
public interface IUnitConversionResolver
{
    /// <summary>Identity (quantity unchanged, <see cref="UnitConversionResolution.Succeeded"/>
    /// true) when <paramref name="unitId"/> is the item's own base unit. Otherwise looks up the
    /// single active conversion for (item, unit) - <see cref="UnitConversionResolution.Succeeded"/>
    /// false when none exists (task 6.6, 409 CONVERSION_NOT_DEFINED).</summary>
    Task<UnitConversionResolution> ResolveAsync(Guid itemId, Guid unitId, decimal quantity, CancellationToken cancellationToken);
}

public sealed record UnitConversionResolution(bool Succeeded, decimal BaseQuantity);
