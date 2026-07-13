using Application.Performance;
using Application.Performance.Services;
using Models;
using SharedKernel.ValueObjects;

namespace Application.Tests;

public sealed class PerformanceCalculatorTests
{
    private readonly PerformanceCalculator _calculator = new();

    [Fact]
    public void Calculate_ReturnsPortfolioAndPositionMetrics()
    {
        var portfolio = PortfolioWith(new Position(new AssetSymbol("PETR4"), new Quantity(10), new Money(10), new Percentage(100)), 100m);
        var asset = AssetWith(new AssetSymbol("PETR4"), 12m, [new( new DateTime(2024, 1, 1), new Money(10)), new(new DateTime(2024, 1, 2), new Money(11))]);

        var result = _calculator.Calculate(portfolio, new Dictionary<AssetSymbol, Asset> { [asset.Symbol] = asset }, new DateTime(2025, 1, 1));

        Assert.Equal(120m, result.CurrentValue);
        Assert.Equal(20m, result.TotalReturn);
        Assert.Equal(20m, result.TotalReturnAmount);
        Assert.NotNull(result.AnnualizedReturn);
        Assert.NotNull(result.Volatility);
        var position = Assert.Single(result.PositionsPerformance);
        Assert.Equal(20m, position.Return);
        Assert.Equal(100m, position.Weight);
    }

    [Fact]
    public void Calculate_ReturnsNullVolatilityWhenNoPriceHistoryExists()
    {
        var portfolio = PortfolioWith(new Position(new AssetSymbol("PETR4"), new Quantity(10), new Money(10), new Percentage(100)), 100m);
        var asset = AssetWith(new AssetSymbol("PETR4"), 10m, []);

        var result = _calculator.Calculate(portfolio, new Dictionary<AssetSymbol, Asset> { [asset.Symbol] = asset }, DateTime.UtcNow);

        Assert.Null(result.Volatility);
    }

    [Fact]
    public void Calculate_UsesPositionInvestmentAndRejectsPartialPriceHistory()
    {
        var petr4 = new Position(new AssetSymbol("PETR4"), new Quantity(10), new Money(10), new Percentage(50));
        var vale3 = new Position(new AssetSymbol("VALE3"), new Quantity(10), new Money(10), new Percentage(50));
        var portfolio = new Portfolio("Test", "user", new Money(1_000m), new DateTime(2024, 1, 1), [petr4, vale3]);
        portfolio.AssignId(1);
        var assets = new Dictionary<AssetSymbol, Asset>
        {
            [petr4.AssetSymbol] = AssetWith(petr4.AssetSymbol, 12m, [new(new DateTime(2024, 1, 1), new Money(10)), new(new DateTime(2024, 1, 2), new Money(11))]),
            [vale3.AssetSymbol] = AssetWith(vale3.AssetSymbol, 12m, [])
        };

        var result = _calculator.Calculate(portfolio, assets, new DateTime(2024, 1, 2));

        Assert.Equal(200m, result.TotalInvestment);
        Assert.Equal(240m, result.CurrentValue);
        Assert.Equal(20m, result.TotalReturn);
        Assert.Null(result.Volatility);
    }

    [Fact]
    public void Calculate_AvoidsDivisionByZeroForZeroInvestment()
    {
        var portfolio = PortfolioWith(new Position(new AssetSymbol("PETR4"), new Quantity(0), new Money(10), new Percentage(100)), 0m);
        var asset = AssetWith(new AssetSymbol("PETR4"), 10m, []);

        var result = _calculator.Calculate(portfolio, new Dictionary<AssetSymbol, Asset> { [asset.Symbol] = asset }, DateTime.UtcNow);

        Assert.Null(result.TotalReturn);
        Assert.Null(result.AnnualizedReturn);
        Assert.Null(Assert.Single(result.PositionsPerformance).Weight);
    }

    [Fact]
    public void Calculate_HandlesZeroCurrentPriceWithoutDividingByIt()
    {
        var portfolio = PortfolioWith(new Position(new AssetSymbol("PETR4"), new Quantity(10), new Money(10), new Percentage(100)), 100m);
        var asset = AssetWith(new AssetSymbol("PETR4"), 0m, []);

        var result = _calculator.Calculate(portfolio, new Dictionary<AssetSymbol, Asset> { [asset.Symbol] = asset }, DateTime.UtcNow);

        Assert.Equal(0m, result.CurrentValue);
        Assert.Equal(-100m, result.TotalReturn);
        Assert.Equal(-100m, Assert.Single(result.PositionsPerformance).Return);
        Assert.Null(Assert.Single(result.PositionsPerformance).Weight);
    }

    private static Portfolio PortfolioWith(Position position, decimal investment)
    {
        var portfolio = new Portfolio("Test", "user", new Money(investment), new DateTime(2024, 1, 1), [position]);
        portfolio.AssignId(1);
        return portfolio;
    }

    private static Asset AssetWith(AssetSymbol symbol, decimal price, IEnumerable<PricePoint> history)
    {
        var asset = new Asset(symbol, "Asset", "Stock", "Sector", new Money(price), new DateTime(2024, 1, 2));
        asset.SetPriceHistory(history);
        return asset;
    }
}
