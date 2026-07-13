using Models;
using Models.Events;
using SharedKernel.Enums;
using SharedKernel.ValueObjects;

namespace Domain.Tests;

public sealed class PortfolioTests
{
    [Fact]
    public void ApplyRebalanceTrade_UpdatesPositionAndRaisesDomainEvent()
    {
        var symbol = new AssetSymbol("PETR4");
        var position = new Position(
            symbol,
            new Quantity(10m),
            new Money(30m),
            new Percentage(100m));
        var portfolio = new Portfolio(
            "Carteira",
            "user-001",
            new Money(300m),
            new DateTime(2024, 1, 1),
            [position]);
        portfolio.AssignId(7);
        portfolio.ClearDomainEvents();
        var transactionDate = new DateTime(2024, 2, 1);

        portfolio.ApplyRebalanceTrade(
            symbol,
            TradeActionEnum.Buy,
            new Quantity(2m),
            new Quantity(12m),
            new Money(31m),
            transactionDate);

        Assert.Equal(12m, position.Quantity.Value);
        Assert.Equal(31m, position.AveragePrice.Value);
        Assert.Equal(transactionDate, position.LastTransaction);
        var domainEvent = Assert.IsType<PositionRebalanced>(Assert.Single(portfolio.DomainEvents));
        Assert.Equal(7, domainEvent.PortfolioId);
        Assert.Equal(symbol, domainEvent.Symbol);
        Assert.Equal(TradeActionEnum.Buy, domainEvent.Action);
        Assert.Equal(2m, domainEvent.TradedQuantity.Value);
    }
}
