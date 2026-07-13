using Application.Rebalancing;

namespace Application.Tests;

public sealed class RebalancingOptimizerTests
{
    private readonly RebalancingOptimizer _optimizer = new();

    [Fact]
    public void Optimize_BalancesPurchasesSalesAndCostsInOnePlan()
    {
        var result = _optimizer.Optimize(
        [
            new RebalancingPosition("PETR4", 5_000m, 100m, 30m),
            new RebalancingPosition("VALE3", 2_500m, 50m, 35m),
            new RebalancingPosition("BBDC4", 2_500m, 50m, 35m)
        ]);

        Assert.True(result.NeedsRebalancing);
        Assert.Equal(3, result.SuggestedTrades.Count);
        var sales = result.SuggestedTrades.Where(trade => trade.Action == "SELL").Sum(trade => trade.EstimatedValue - trade.TransactionCost);
        var purchases = result.SuggestedTrades.Where(trade => trade.Action == "BUY").Sum(trade => trade.EstimatedValue + trade.TransactionCost);
        Assert.True(sales >= purchases);
        Assert.All(result.SuggestedTrades, trade => Assert.True(trade.EstimatedValue >= 100m));
        Assert.All(result.SuggestedTrades, trade => Assert.Equal(decimal.Round(trade.EstimatedValue * 0.003m, 2, MidpointRounding.AwayFromZero), trade.TransactionCost));

        var finalValues = new Dictionary<string, decimal>
        {
            ["PETR4"] = 5_000m,
            ["VALE3"] = 2_500m,
            ["BBDC4"] = 2_500m
        };
        foreach (var trade in result.SuggestedTrades)
            finalValues[trade.Symbol] += trade.Action == "BUY" ? trade.EstimatedValue : -trade.EstimatedValue;
        var postCostValue = 10_000m - result.TotalTransactionCost;
        Assert.InRange(finalValues["PETR4"] / postCostValue * 100m, 29.9m, 30.1m);
        Assert.InRange(finalValues["VALE3"] / postCostValue * 100m, 34.9m, 35.1m);
        Assert.InRange(finalValues["BBDC4"] / postCostValue * 100m, 34.9m, 35.1m);
        Assert.NotNull(result.Optimization);
        Assert.Equal(3, result.Optimization.Alternatives.Count);
        Assert.Contains(result.Optimization.Alternatives, alternative => alternative.Strategy == "Exhaustive");
        Assert.Contains(result.Optimization.Alternatives, alternative => alternative.Strategy == "QuadraticProgramming");
        Assert.Contains(result.Optimization.Alternatives, alternative => alternative.Strategy == "CpSat");
    }

    [Fact]
    public void Optimize_NormalizesTargetsThatDoNotSumToOneHundredPercent()
    {
        var result = _optimizer.Optimize(
        [
            new RebalancingPosition("PETR4", 8_000m, 100m, 60m),
            new RebalancingPosition("VALE3", 2_000m, 100m, 20m)
        ]);

        var sale = Assert.Single(result.SuggestedTrades, trade => trade.Action == "SELL");
        Assert.Equal("PETR4", sale.Symbol);
        Assert.True(result.NeedsRebalancing);
    }

    [Fact]
    public void Optimize_ReturnsNoPlanWhenTargetsAreMissing()
    {
        var result = _optimizer.Optimize([new RebalancingPosition("PETR4", 1_000m, 100m, 0m)]);

        Assert.False(result.NeedsRebalancing);
        Assert.Empty(result.SuggestedTrades);
    }

    [Theory]
    [InlineData(RebalancingOptimizationMode.Exhaustive, "Exhaustive")]
    [InlineData(RebalancingOptimizationMode.QuadraticProgramming, "QuadraticProgramming")]
    [InlineData(RebalancingOptimizationMode.CpSat, "CpSat")]
    public void Optimize_WithSpecificMode_ExecutesOnlySelectedStrategy(
        RebalancingOptimizationMode mode,
        string expectedStrategy)
    {
        var result = _optimizer.Optimize(
        [
            new RebalancingPosition("PETR4", 5_000m, 100m, 30m),
            new RebalancingPosition("VALE3", 2_500m, 50m, 35m),
            new RebalancingPosition("BBDC4", 2_500m, 50m, 35m)
        ], mode);

        var alternative = Assert.Single(result.Optimization!.Alternatives);
        Assert.Equal(expectedStrategy, alternative.Strategy);
        Assert.Equal("Succeeded", alternative.Status);
        Assert.True(alternative.Metrics.IsSelfFinanced);
    }

    [Fact]
    public void Optimize_IsDeterministicForTheSameProblem()
    {
        var positions = new[]
        {
            new RebalancingPosition("PETR4", 5_000m, 100m, 30m),
            new RebalancingPosition("VALE3", 2_500m, 50m, 35m),
            new RebalancingPosition("BBDC4", 2_500m, 50m, 35m)
        };

        var first = _optimizer.Optimize(positions);
        var second = _optimizer.Optimize(positions);

        Assert.Equal(first.Optimization!.SelectedStrategy, second.Optimization!.SelectedStrategy);
        Assert.Equal(first.TotalTransactionCost, second.TotalTransactionCost);
        Assert.Equal(first.SuggestedTrades, second.SuggestedTrades);
    }
}
