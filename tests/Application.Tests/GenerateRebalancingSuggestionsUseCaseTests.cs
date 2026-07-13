using System.Linq.Expressions;
using Application.Contracts;
using Application.Exceptions;
using Application.Rebalancing;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using SharedKernel.ValueObjects;

namespace Application.Tests;

public sealed class GenerateRebalancingSuggestionsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsASelfFinancedPlanForMaterialDeviations()
    {
        var portfolio = PortfolioWith(
            10_000m,
            Position("PETR4", 50, 100m, 30m),
            Position("VALE3", 50, 50m, 35m),
            Position("BBDC4", 50, 50m, 35m));
        var service = CreateService(portfolio, Asset("PETR4", 100m), Asset("VALE3", 50m), Asset("BBDC4", 50m));

        var result = await service.ExecuteAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.True(result.NeedsRebalancing);
        Assert.Collection(
            result.CurrentAllocation,
            allocation => Assert.Equal(("PETR4", 50m, 30m, 20m), (allocation.Symbol, allocation.CurrentWeight, allocation.TargetWeight, allocation.Deviation)),
            allocation => Assert.Equal(("BBDC4", 25m, 35m, -10m), (allocation.Symbol, allocation.CurrentWeight, allocation.TargetWeight, allocation.Deviation)),
            allocation => Assert.Equal(("VALE3", 25m, 35m, -10m), (allocation.Symbol, allocation.CurrentWeight, allocation.TargetWeight, allocation.Deviation)));
        Assert.Equal(3, result.SuggestedTrades.Count);
        Assert.All(result.SuggestedTrades, trade => Assert.True(trade.EstimatedValue >= 100m));
        Assert.All(result.SuggestedTrades, trade => Assert.Equal(decimal.Round(trade.EstimatedValue * 0.003m, 2, MidpointRounding.AwayFromZero), trade.TransactionCost));
        var sales = result.SuggestedTrades.Where(trade => trade.Action == "SELL").Sum(trade => trade.EstimatedValue - trade.TransactionCost);
        var purchases = result.SuggestedTrades.Where(trade => trade.Action == "BUY").Sum(trade => trade.EstimatedValue + trade.TransactionCost);
        Assert.True(sales >= purchases);
        Assert.Equal(result.SuggestedTrades.Sum(trade => trade.TransactionCost), result.TotalTransactionCost);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSuggestTradesAtDeviationThresholdOrBelowMinimumValue()
    {
        var portfolio = PortfolioWith(
            1_000m,
            Position("PETR4", 5, 100m, 48m),
            Position("VALE3", 5, 100m, 52m));
        var service = CreateService(portfolio, Asset("PETR4", 100m), Asset("VALE3", 100m));

        var result = await service.ExecuteAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.False(result.NeedsRebalancing);
        Assert.Empty(result.SuggestedTrades);
        Assert.Equal(0m, result.TotalTransactionCost);
        Assert.Equal("Nenhuma operação atende aos critérios de rebalanceamento.", result.ExpectedImprovement);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsAssetsWithZeroPriceWithoutFailing()
    {
        var portfolio = PortfolioWith(
            1_000m,
            Position("PETR4", 1, 0m, 50m),
            Position("VALE3", 10, 100m, 50m));
        var service = CreateService(portfolio, Asset("PETR4", 0m), Asset("VALE3", 100m));

        var result = await service.ExecuteAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Single(result.SuggestedTrades);
        Assert.Equal("VALE3", result.SuggestedTrades[0].Symbol);
        Assert.Equal("SELL", result.SuggestedTrades[0].Action);
        Assert.Equal(5.0075m, result.SuggestedTrades[0].Quantity);
    }

    [Fact]
    public async Task ExecuteAsync_RoundsTransactionCostPerTradeAwayFromZero()
    {
        var portfolio = PortfolioWith(
            17_750m,
            Position("PETR4", 250, 35.50m, 40m),
            Position("VALE3", 250, 35.50m, 60m));
        var service = CreateService(portfolio, Asset("PETR4", 35.50m), Asset("VALE3", 35.50m));

        var result = await service.ExecuteAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.All(result.SuggestedTrades, trade =>
            Assert.Equal(decimal.Round(trade.EstimatedValue * 0.003m, 2, MidpointRounding.AwayFromZero), trade.TransactionCost));
        Assert.Equal(result.SuggestedTrades.Sum(trade => trade.TransactionCost), result.TotalTransactionCost);
    }

    [Fact]
    public async Task ExecuteAsync_DiscardsSubMinimumPurchaseWhileKeepingMaterialTrade()
    {
        var portfolio = PortfolioWith(
            1_000m,
            Position("PETR4", 10, 60m, 50m),
            Position("VALE3", 10, 40m, 50m));
        var service = CreateService(portfolio, Asset("PETR4", 60m), Asset("VALE3", 40m));

        var result = await service.ExecuteAsync(portfolio.Id);

        Assert.NotNull(result);
        var sell = Assert.Single(result.SuggestedTrades);
        Assert.Equal("PETR4", sell.Symbol);
        Assert.Equal("SELL", sell.Action);
        Assert.True(sell.EstimatedValue >= 100m);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNullWhenPortfolioDoesNotExist()
    {
        var service = CreateService(null);

        Assert.Null(await service.ExecuteAsync(99));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSuggestTradesWhenPortfolioCurrentValueIsZero()
    {
        var portfolio = PortfolioWith(0m, Position("PETR4", 1m, 0m, 100m));
        var service = CreateService(portfolio, Asset("PETR4", 0m));

        var result = await service.ExecuteAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.False(result.NeedsRebalancing);
        Assert.Empty(result.SuggestedTrades);
        Assert.Equal("Nenhuma operação atende aos critérios de rebalanceamento.", result.ExpectedImprovement);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsIncompletePortfolioDataWhenAnAssetIsMissing()
    {
        var portfolio = PortfolioWith(100m, Position("PETR4", 1, 100m, 100m));
        var service = CreateService(portfolio);

        var exception = await Assert.ThrowsAsync<PortfolioDataIncompleteException>(() => service.ExecuteAsync(portfolio.Id));

        Assert.Contains("PETR4", exception.Message);
    }

    private static GenerateRebalancingSuggestionsUseCase CreateService(Portfolio? portfolio, params Asset[] assets) =>
        new(new PortfolioRepositoryStub(portfolio), new AssetRepositoryStub(assets), new RebalancingOptimizer(), NullLogger<GenerateRebalancingSuggestionsUseCase>.Instance);

    private static Portfolio PortfolioWith(decimal totalInvestment, params Position[] positions)
    {
        var portfolio = new Portfolio("Test", "user", new Money(totalInvestment), new DateTime(2024, 1, 1), positions);
        portfolio.AssignId(1);
        return portfolio;
    }

    private static Position Position(string symbol, decimal quantity, decimal averagePrice, decimal targetAllocation) =>
        new(new AssetSymbol(symbol), new Quantity(quantity), new Money(averagePrice), new Percentage(targetAllocation));

    private static Asset Asset(string symbol, decimal price) =>
        new(new AssetSymbol(symbol), symbol, "Stock", "Sector", new Money(price), new DateTime(2024, 1, 1));

    private sealed class PortfolioRepositoryStub(Portfolio? portfolio) : IPortfolioPositionsReader
    {
        public Task<Portfolio?> GetWithPositionsAsync(int id, CancellationToken ct = default) => Task.FromResult(portfolio is not null && id == portfolio.Id ? portfolio : null);
        public Task<Portfolio?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> GetAllAsync(Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> QueryAsync(Expression<Func<Portfolio, bool>> predicate, Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Portfolio?> QuerySingleAsync(Expression<Func<Portfolio, bool>> predicate, Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Portfolio entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Portfolio entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AssetRepositoryStub(IEnumerable<Asset> assets) : IAssetReader
    {
        private readonly IReadOnlyDictionary<AssetSymbol, Asset> _assets = assets.ToDictionary(asset => asset.Symbol);

        public Task<Asset?> GetByIdAsync(AssetSymbol id, CancellationToken ct = default) => Task.FromResult(_assets.GetValueOrDefault(id));
        public Task<Asset?> GetWithPriceHistoryAsync(AssetSymbol symbol, CancellationToken ct = default) => Task.FromResult(_assets.GetValueOrDefault(symbol));
        public Task<IReadOnlyList<Asset>> GetAllAsync(Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Asset>> QueryAsync(Expression<Func<Asset, bool>> predicate, Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Asset?> QuerySingleAsync(Expression<Func<Asset, bool>> predicate, Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Asset entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Asset entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(AssetSymbol id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
