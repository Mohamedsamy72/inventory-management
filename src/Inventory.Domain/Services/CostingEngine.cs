namespace Inventory.Domain.Services;

/// <summary>See <see cref="ICostingEngine"/>.</summary>
public sealed class CostingEngine : ICostingEngine
{
    public CostingResult RecomputeWeightedAverage(decimal currentQuantity, decimal currentAverageCost, decimal deltaQuantity, decimal deltaUnitCost)
    {
        decimal newQuantity = currentQuantity + deltaQuantity;

        // WAC is undefined over zero units - a full reversal of the only receipt on hand must
        // not divide by zero, and there is nothing meaningful to report as "the average" of an
        // empty balance anyway.
        decimal newAverageCost = newQuantity == 0m
            ? 0m
            : ((currentQuantity * currentAverageCost) + (deltaQuantity * deltaUnitCost)) / newQuantity;

        return new CostingResult(newQuantity, newAverageCost, deltaUnitCost, deltaQuantity * deltaUnitCost);
    }

    public CostingResult ValueAtCurrentCost(decimal currentQuantity, decimal currentAverageCost, decimal deltaQuantity)
    {
        decimal newQuantity = currentQuantity + deltaQuantity;
        return new CostingResult(newQuantity, currentAverageCost, currentAverageCost, deltaQuantity * currentAverageCost);
    }
}
