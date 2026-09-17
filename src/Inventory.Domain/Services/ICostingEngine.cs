namespace Inventory.Domain.Services;

/// <summary>
/// Task 7.7 - implements ADR-020 exactly. Pure arithmetic, no I/O: the caller resolves which
/// movement types recompute the Weighted Average Cost and which do not (that decision belongs to
/// <c>IStockPostingService</c>, which has the movement-type context this engine deliberately does
/// not need), and passes in only the numbers. Unit-testable in complete isolation, as ADR-020's
/// own "Consequences" line requires.
/// </summary>
public interface ICostingEngine
{
    /// <summary>OPENING_BALANCE, INCOMING_POSTED, and INCOMING_RECONCILIATION (ADR-020): blends
    /// <paramref name="deltaQuantity"/> at <paramref name="deltaUnitCost"/> into the existing
    /// balance via the standard Weighted Average Cost formula. <paramref name="deltaQuantity"/>
    /// may be negative (a reconciliation reducing an over-counted receipt) - the result's
    /// <see cref="CostingResult.NewAverageCost"/> is 0 when the resulting quantity is exactly
    /// zero, since WAC is undefined over zero units and must never divide by zero.</summary>
    CostingResult RecomputeWeightedAverage(decimal currentQuantity, decimal currentAverageCost, decimal deltaQuantity, decimal deltaUnitCost);

    /// <summary>RESTAURANT_RECEIPT_CONFIRMED and PHYSICAL_ADJUSTMENT (ADR-020): values the
    /// movement at the CURRENT average cost and leaves it unchanged - only
    /// <see cref="CostingResult.NewQuantity"/> moves.</summary>
    CostingResult ValueAtCurrentCost(decimal currentQuantity, decimal currentAverageCost, decimal deltaQuantity);
}

/// <summary>The result of applying one movement to a balance. <see cref="MovementUnitCost"/>/
/// <see cref="MovementTotalCost"/> are what the ledger row itself records (docs/25: "a cost not
/// captured at posting time is unrecoverable forever") - not necessarily the same as the new
/// running average.</summary>
public sealed record CostingResult(decimal NewQuantity, decimal NewAverageCost, decimal MovementUnitCost, decimal MovementTotalCost);
