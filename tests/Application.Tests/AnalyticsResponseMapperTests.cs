using Application.Mappings;
using Application.Performance;
using Application.Rebalancing;
using Application.Risk;

namespace Application.Tests;

public sealed class AnalyticsResponseMapperTests
{
    [Fact]
    public void ToResponse_MapsPerformanceResultAndPositions()
    {
        var source = new PortfolioPerformanceResult(
            1_000m, 1_150m, 15m, 150m, 14.2m, 8.4m,
            [new PositionPerformanceResult("PETR4", 1_000m, 1_150m, 15m, 100m)]);

        var result = AnalyticsResponseMapper.ToResponse(source);

        Assert.Equal((1_000m, 1_150m, 15m, 150m, 14.2m, 8.4m),
            (result.TotalInvestment, result.CurrentValue, result.TotalReturn, result.TotalReturnAmount, result.AnnualizedReturn, result.Volatility));
        var position = Assert.Single(result.PositionsPerformance);
        Assert.Equal(("PETR4", 1_000m, 1_150m, 15m, 100m),
            (position.Symbol, position.InvestedAmount, position.CurrentValue, position.Return, position.Weight));
    }

    [Fact]
    public void ToResponse_MapsRiskResultIncludingNestedObjectsAndCollections()
    {
        var source = new RiskAnalysisResult(
            "Medium", 1.25m,
            new ConcentrationRiskResult(new LargestPositionRiskResult("VALE3", 42m), 70m),
            [new SectorDiversificationResult("Materials", 42m, "High")],
            ["Diversify the portfolio."]);

        var result = AnalyticsResponseMapper.ToResponse(source);

        Assert.Equal("Medium", result.OverallRisk);
        Assert.Equal(1.25m, result.SharpeRatio);
        Assert.NotNull(result.ConcentrationRisk.LargestPosition);
        Assert.Equal(("VALE3", 42m, 70m),
            (result.ConcentrationRisk.LargestPosition.Symbol, result.ConcentrationRisk.LargestPosition.Percentage, result.ConcentrationRisk.Top3Concentration));
        var sector = Assert.Single(result.SectorDiversification);
        Assert.Equal(("Materials", 42m, "High"), (sector.Sector, sector.Percentage, sector.Risk));
        Assert.Equal(["Diversify the portfolio."], result.Recommendations);
    }

    [Fact]
    public void ToResponse_MapsRiskResultWithNullLargestPosition()
    {
        var source = new RiskAnalysisResult(
            "Low", null,
            new ConcentrationRiskResult(null, 0m),
            [], []);

        var result = AnalyticsResponseMapper.ToResponse(source);

        Assert.Null(result.SharpeRatio);
        Assert.Null(result.ConcentrationRisk.LargestPosition);
        Assert.Empty(result.SectorDiversification);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void ToResponse_MapsRebalancingResultAndCollections()
    {
        var source = new RebalancingResult(
            true,
            [new CurrentAllocationResult("PETR4", 60m, 50m, 10m)],
            [new SuggestedTradeResult("PETR4", "SELL", 2m, 200m, 0.6m, "Reduce allocation.")],
            0.6m,
            "Lower concentration.");

        var result = AnalyticsResponseMapper.ToResponse(source);

        Assert.True(result.NeedsRebalancing);
        var allocation = Assert.Single(result.CurrentAllocation);
        Assert.Equal(("PETR4", 60m, 50m, 10m), (allocation.Symbol, allocation.CurrentWeight, allocation.TargetWeight, allocation.Deviation));
        var trade = Assert.Single(result.SuggestedTrades);
        Assert.Equal(("PETR4", "SELL", 2m, 200m, 0.6m, "Reduce allocation."),
            (trade.Symbol, trade.Action, trade.Quantity, trade.EstimatedValue, trade.TransactionCost, trade.Reason));
        Assert.Equal((0.6m, "Lower concentration."), (result.TotalTransactionCost, result.ExpectedImprovement));
    }
}
