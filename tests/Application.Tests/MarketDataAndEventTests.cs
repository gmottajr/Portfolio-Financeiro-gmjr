using Application.Rebalancing;
using Models;
using Models.Events;
using SharedKernel.Enums;
using SharedKernel.ValueObjects;

namespace Application.Tests;

public sealed class MarketDataAndEventTests
{
    [Fact]
    public void MarketData_CopiesIndexesAndExposesSectorInformation()
    {
        var indexes = new Dictionary<string, IndexPerformance>
        {
            ["IBOV"] = new(130_245.67m, new Percentage(1.2m), new Percentage(2.3m), new Percentage(8.4m))
        };
        var sectors = new[]
        {
            new SectorInfo("Energy", new ReturnRate(10.5m), new Volatility(18.2m), [new AssetSymbol("PETR4")])
        };

        var marketData = new MarketData(new Percentage(12m), indexes, sectors);
        indexes.Clear();

        Assert.Equal(12m, marketData.SelicRate.Value);
        Assert.Equal(130_245.67m, marketData.IndexPerformance["IBOV"].CurrentValue);
        var sector = Assert.Single(marketData.Sectors);
        Assert.Equal("Energy", sector.Name);
        Assert.Equal(10.5m, sector.AverageReturn.Value.Value);
        Assert.Equal(18.2m, sector.Volatility.Value.Value);
        Assert.Equal("PETR4", Assert.Single(sector.Assets).Value);
    }

    [Fact]
    public void DomainEvents_ExposeTheDataThatDescribesTheirChange()
    {
        var symbol = new AssetSymbol("PETR4");
        var priceUpdated = new AssetPriceUpdated(symbol, new Money(30m), new Money(35.5m));
        var rebalanced = new PositionRebalanced(12, symbol, TradeActionEnum.Sell, new Quantity(50m));
        var allocationChanged = new TargetAllocationChanged(12, symbol, new Percentage(20m), new Percentage(15m));

        Assert.Equal(("PETR4", 30m, 35.5m), (priceUpdated.Symbol.Value, priceUpdated.OldPrice.Value, priceUpdated.NewPrice.Value));
        Assert.Equal((12, "PETR4", TradeActionEnum.Sell, 50m), (rebalanced.PortfolioId, rebalanced.Symbol.Value, rebalanced.Action, rebalanced.TradedQuantity.Value));
        Assert.Equal((12, "PETR4", 20m, 15m), (allocationChanged.PortfolioId, allocationChanged.Symbol.Value, allocationChanged.OldAllocation.Value, allocationChanged.NewAllocation.Value));
        Assert.True(priceUpdated.OccurredOn <= DateTime.UtcNow);
    }

    [Fact]
    public void SuggestedTrade_PreservesAllResponseFields()
    {
        var trade = new SuggestedTrade("PETR4", TradeActionEnum.Sell, 50m, 1_775m, 5.33m, "Reduzir concentração.");

        Assert.Equal("PETR4", trade.Symbol);
        Assert.Equal(TradeActionEnum.Sell, trade.Action);
        Assert.Equal(50m, trade.Quantity);
        Assert.Equal(1_775m, trade.EstimatedValue);
        Assert.Equal(5.33m, trade.TransactionCost);
        Assert.Equal("Reduzir concentração.", trade.Reason);
    }
}
