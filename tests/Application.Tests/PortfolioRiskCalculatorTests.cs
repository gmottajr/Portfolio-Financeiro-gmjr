using Application.Risk;
using Models;
using SharedKernel.ValueObjects;

namespace Application.Tests;

public sealed class PortfolioRiskCalculatorTests
{
    private readonly PortfolioRiskCalculator _calculator = new();

    [Fact]
    public void Calculate_WithoutPositions_ReturnsLowRiskAndNoMetrics()
    {
        var result = _calculator.Calculate([], new DateTime(2024, 1, 1), 10m);

        Assert.Equal("Low", result.OverallRisk);
        Assert.Null(result.SharpeRatio);
        Assert.Null(result.ConcentrationRisk.LargestPosition);
        Assert.Equal(0m, result.ConcentrationRisk.Top3Concentration);
        Assert.Empty(result.SectorDiversification);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public void Calculate_ConcentratedPortfolio_ClassifiesRiskAndBuildsRecommendations()
    {
        var positions = new[]
        {
            Position("PETR4", "Energy", 60m),
            Position("VALE3", "Mining", 25m),
            Position("ITUB4", "Financial", 15m)
        };

        var result = _calculator.Calculate(positions, new DateTime(2024, 1, 1), 10m);

        Assert.Equal("High", result.OverallRisk);
        Assert.Equal("PETR4", result.ConcentrationRisk.LargestPosition!.Symbol);
        Assert.Equal(60m, result.ConcentrationRisk.LargestPosition.Percentage);
        Assert.Equal(100m, result.ConcentrationRisk.Top3Concentration);
        var energy = Assert.Single(result.SectorDiversification, item => item.Sector == "Energy");
        Assert.Equal("High", energy.Risk);
        Assert.Contains(result.Recommendations, item => item.Contains("setor Energy"));
        Assert.Contains(result.Recommendations, item => item.Contains("posição PETR4"));
        Assert.Contains(result.Recommendations, item => item.StartsWith("Diversificar"));
    }

    [Fact]
    public void Calculate_WithCompletePriceHistory_CalculatesSharpeRatio()
    {
        var asset = new Asset(new AssetSymbol("PETR4"), "Petrobras", "Stock", "Energy", new Money(120m), new DateTime(2025, 1, 1));
        asset.SetPriceHistory([
            new PricePoint(new DateTime(2024, 1, 1), new Money(100m)),
            new PricePoint(new DateTime(2024, 1, 2), new Money(110m)),
            new PricePoint(new DateTime(2024, 1, 3), new Money(100m))
        ]);

        var result = _calculator.Calculate([new RiskPositionValue("PETR4", asset, 100m, 120m)], new DateTime(2024, 1, 1), 10m);

        Assert.Equal(0.0656m, result.SharpeRatio);
    }

    [Fact]
    public void Calculate_WhenAnyPositionLacksHistory_ReturnsNoSharpeRatio()
    {
        var withHistory = Position("PETR4", "Energy", 100m, [100m, 110m]);
        var withoutHistory = Position("VALE3", "Mining", 100m);

        var result = _calculator.Calculate([withHistory, withoutHistory], new DateTime(2024, 1, 1), 10m);

        Assert.Null(result.SharpeRatio);
    }

    private static RiskPositionValue Position(string symbol, string sector, decimal value, IEnumerable<decimal>? history = null)
    {
        var asset = new Asset(new AssetSymbol(symbol), symbol, "Stock", sector, new Money(value), new DateTime(2025, 1, 1));
        if (history is not null)
            asset.SetPriceHistory(history.Select((price, index) => new PricePoint(new DateTime(2024, 1, 1).AddDays(index), new Money(price))));
        return new RiskPositionValue(symbol, asset, value, value);
    }
}
