using Inventory.Domain.Services;
using Xunit;

namespace Inventory.UnitTests;

/// <summary>Task 7.14 (ADR-020) - pure arithmetic, no database, exactly the docs/09 examples.</summary>
public sealed class CostingEngineTests
{
    private readonly CostingEngine _engine = new();

    [Fact]
    public void Incoming_Posted_Adds_Quantity_500_Plus_100_Equals_600()
    {
        CostingResult result = _engine.RecomputeWeightedAverage(currentQuantity: 500m, currentAverageCost: 10m, deltaQuantity: 100m, deltaUnitCost: 10m);

        Assert.Equal(600m, result.NewQuantity);
    }

    [Fact]
    public void Reconciliation_Of_Minus_Twenty_Reduces_Six_Hundred_To_Five_Hundred_Eighty()
    {
        CostingResult result = _engine.RecomputeWeightedAverage(currentQuantity: 600m, currentAverageCost: 10m, deltaQuantity: -20m, deltaUnitCost: 10m);

        Assert.Equal(580m, result.NewQuantity);
    }

    [Fact]
    public void Confirmation_Of_Minus_Eighteen_Reduces_Five_Hundred_Eighty_To_Five_Hundred_Sixty_Two()
    {
        CostingResult result = _engine.ValueAtCurrentCost(currentQuantity: 580m, currentAverageCost: 10m, deltaQuantity: -18m);

        Assert.Equal(562m, result.NewQuantity);
    }

    [Fact]
    public void Weighted_Average_Of_100_At_10_Plus_50_At_16_Is_12_Exactly()
    {
        // (100*10 + 50*16) / 150 = (1000 + 800) / 150 = 12.0000
        CostingResult result = _engine.RecomputeWeightedAverage(currentQuantity: 100m, currentAverageCost: 10m, deltaQuantity: 50m, deltaUnitCost: 16m);

        Assert.Equal(150m, result.NewQuantity);
        Assert.Equal(12.0000m, result.NewAverageCost);
    }

    [Fact]
    public void Weighted_Average_Cost_Is_Unchanged_By_An_Issue()
    {
        CostingResult result = _engine.ValueAtCurrentCost(currentQuantity: 100m, currentAverageCost: 12m, deltaQuantity: -30m);

        Assert.Equal(12m, result.NewAverageCost);
    }

    [Fact]
    public void Reconciliation_Values_The_Movement_At_The_Lines_Own_Cost_Not_The_Current_Wac()
    {
        // The current WAC (12) is irrelevant to what THIS movement is recorded at - the
        // reconciliation corrects a specific receipt originally posted at 10.
        CostingResult result = _engine.RecomputeWeightedAverage(currentQuantity: 150m, currentAverageCost: 12m, deltaQuantity: -10m, deltaUnitCost: 10m);

        Assert.Equal(10m, result.MovementUnitCost);
        Assert.Equal(-100m, result.MovementTotalCost);
    }

    [Fact]
    public void Adjustment_Values_The_Movement_At_The_Current_Wac()
    {
        CostingResult result = _engine.ValueAtCurrentCost(currentQuantity: 100m, currentAverageCost: 12m, deltaQuantity: -7m);

        Assert.Equal(12m, result.MovementUnitCost);
        Assert.Equal(-84m, result.MovementTotalCost);
    }

    [Fact]
    public void Fully_Reversing_The_Only_Receipt_Leaves_Average_Cost_At_Zero_Not_A_Division_By_Zero()
    {
        CostingResult result = _engine.RecomputeWeightedAverage(currentQuantity: 100m, currentAverageCost: 10m, deltaQuantity: -100m, deltaUnitCost: 10m);

        Assert.Equal(0m, result.NewQuantity);
        Assert.Equal(0m, result.NewAverageCost);
    }
}
