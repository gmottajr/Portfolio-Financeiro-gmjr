using System.Linq.Expressions;
using Application.Contracts;
using Application.Exceptions;
using Application.Risk;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using SharedKernel.ValueObjects;

namespace Application.Tests;

public sealed class RiskAnalysisAppServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_CalculatesSharpeRatioWhenPortfolioReturnSelicAndVolatilityExist()
    {
        var portfolio = PortfolioWith(
            100m,
            new Position(new AssetSymbol("PETR4"), new Quantity(1), new Money(100), new Percentage(100)));
        var asset = AssetWith("PETR4", "Energy", 120m, [100m, 110m, 100m]);
        var service = CreateService(portfolio, [asset], 10m);

        var result = await service.AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Equal(0.0656m, result.SharpeRatio);
        Assert.Equal("High", result.OverallRisk);
        Assert.Equal("PETR4", result.ConcentrationRisk.LargestPosition!.Symbol);
        Assert.Equal(100m, result.ConcentrationRisk.Top3Concentration);
    }

    [Fact]
    public async Task AnalyzeAsync_ClassifiesRiskAndRecommendsReductionForConcentratedSector()
    {
        var positions = new[] { "PETR4", "VALE3", "BBDC4", "ITUB4", "MGLU3" }
            .Select(symbol => new Position(new AssetSymbol(symbol), new Quantity(1), new Money(100), new Percentage(20)))
            .ToList();
        var portfolio = new Portfolio("Test", "user", new Money(500), new DateTime(2024, 1, 1), positions);
        portfolio.AssignId(1);
        var assets = new[]
        {
            AssetWith("PETR4", "Energy", 100m), AssetWith("VALE3", "Energy", 100m), AssetWith("BBDC4", "Energy", 100m),
            AssetWith("ITUB4", "Financial", 100m), AssetWith("MGLU3", "Financial", 100m)
        };
        var service = CreateService(portfolio, assets, 10m);

        var result = await service.AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Null(result.SharpeRatio);
        Assert.Equal("High", result.OverallRisk);
        Assert.Equal(20m, result.ConcentrationRisk.LargestPosition!.Percentage);
        Assert.Equal(60m, result.ConcentrationRisk.Top3Concentration);
        var energy = Assert.Single(result.SectorDiversification, sector => sector.Sector == "Energy");
        Assert.Equal(60m, energy.Percentage);
        Assert.Equal("High", energy.Risk);
        Assert.Contains(result.Recommendations, recommendation => recommendation.Contains("Reduzir exposição ao setor Energy (60.0%)"));
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNullWhenPortfolioDoesNotExist()
    {
        var service = CreateService(null, [], 10m);

        var result = await service.AnalyzeAsync(99);

        Assert.Null(result);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNullSharpeRatioWhenARequiredMetricIsUnavailable()
    {
        var portfolio = PortfolioWith(
            100m,
            new Position(new AssetSymbol("PETR4"), new Quantity(1), new Money(100), new Percentage(100)));
        var assetWithHistory = AssetWith("PETR4", "Energy", 120m, [100m, 110m, 100m]);
        var assetWithoutHistory = AssetWith("PETR4", "Energy", 120m);
        var assetWithZeroVolatility = AssetWith("PETR4", "Energy", 120m, [100m, 110m]);

        var noSelic = await CreateService(portfolio, [assetWithHistory], null).AnalyzeAsync(portfolio.Id);
        var noVolatility = await CreateService(portfolio, [assetWithoutHistory], 10m).AnalyzeAsync(portfolio.Id);
        var zeroVolatility = await CreateService(portfolio, [assetWithZeroVolatility], 10m).AnalyzeAsync(portfolio.Id);
        var noReturn = await CreateService(
                PortfolioWith(0m, new Position(new AssetSymbol("PETR4"), new Quantity(1), new Money(0), new Percentage(100))),
                [assetWithHistory],
                10m)
            .AnalyzeAsync(1);

        Assert.Null(noSelic!.SharpeRatio);
        Assert.Null(noVolatility!.SharpeRatio);
        Assert.Null(zeroVolatility!.SharpeRatio);
        Assert.Null(noReturn!.SharpeRatio);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsLowRiskForPortfolioWithoutPositions()
    {
        var portfolio = PortfolioWith(0m);

        var result = await CreateService(portfolio, [], null).AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Equal("Low", result.OverallRisk);
        Assert.Null(result.SharpeRatio);
        Assert.Null(result.ConcentrationRisk.LargestPosition);
        Assert.Equal(0m, result.ConcentrationRisk.Top3Concentration);
        Assert.Empty(result.SectorDiversification);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesZeroCurrentPriceWithoutDividingByIt()
    {
        var portfolio = PortfolioWith(
            100m,
            new Position(new AssetSymbol("PETR4"), new Quantity(1), new Money(100), new Percentage(100)));
        var service = CreateService(portfolio, [AssetWith("PETR4", "Energy", 0m)], 10m);

        var result = await service.AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Equal("Low", result.OverallRisk);
        Assert.Equal(0m, result.ConcentrationRisk.LargestPosition!.Percentage);
        Assert.Null(result.SharpeRatio);
    }

    [Fact]
    public async Task AnalyzeAsync_ClassifiesMediumRiskAtPositionThresholdAndBuildsMonitoringRecommendation()
    {
        var symbols = new[] { "PETR4", "VALE3", "BBDC4", "ITUB4", "MGLU3" };
        var portfolio = PortfolioWith(
            500m,
            symbols.Select(symbol => new Position(new AssetSymbol(symbol), new Quantity(1), new Money(100), new Percentage(20))).ToArray());
        var assets = symbols.Select((symbol, index) => AssetWith(symbol, $"Sector{index}", 100m));

        var result = await CreateService(portfolio, assets, 10m).AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Equal("Medium", result.OverallRisk);
        Assert.All(result.SectorDiversification, sector => Assert.Equal("Low", sector.Risk));
        Assert.Contains(result.Recommendations, recommendation => recommendation.Contains("Monitorar concentração na posição"));
    }

    [Fact]
    public async Task AnalyzeAsync_ClassifiesLowRiskAndDoesNotRecommendChangesForWellDiversifiedPortfolio()
    {
        var symbols = new[] { "PETR4", "VALE3", "BBDC4", "ITUB4", "MGLU3", "WEGE3", "RENT3", "ABEV3" };
        var portfolio = PortfolioWith(
            800m,
            symbols.Select(symbol => new Position(new AssetSymbol(symbol), new Quantity(1), new Money(100), new Percentage(12.5m))).ToArray());
        var assets = symbols.Select((symbol, index) => AssetWith(symbol, $"Sector{index}", 100m));

        var result = await CreateService(portfolio, assets, 10m).AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Equal("Low", result.OverallRisk);
        Assert.Equal(37.5m, result.ConcentrationRisk.Top3Concentration);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task AnalyzeAsync_RecommendsDiversificationWhenTopThreePositionsExceedSixtyPercent()
    {
        var portfolio = PortfolioWith(
            1000m,
            new Position(new AssetSymbol("PETR4"), new Quantity(4), new Money(100), new Percentage(40)),
            new Position(new AssetSymbol("VALE3"), new Quantity(3), new Money(100), new Percentage(30)),
            new Position(new AssetSymbol("BBDC4"), new Quantity(2), new Money(100), new Percentage(20)),
            new Position(new AssetSymbol("ITUB4"), new Quantity(1), new Money(100), new Percentage(10)));
        var assets = new[]
        {
            AssetWith("PETR4", "Energy", 100m), AssetWith("VALE3", "Mining", 100m),
            AssetWith("BBDC4", "Financial", 100m), AssetWith("ITUB4", "Retail", 100m)
        };

        var result = await CreateService(portfolio, assets, 10m).AnalyzeAsync(portfolio.Id);

        Assert.NotNull(result);
        Assert.Equal(90m, result.ConcentrationRisk.Top3Concentration);
        Assert.Contains(result.Recommendations, recommendation => recommendation.StartsWith("Diversificar o portfólio:"));
    }

    [Fact]
    public async Task AnalyzeAsync_ThrowsWhenAPositionAssetIsMissing()
    {
        var portfolio = PortfolioWith(
            100m,
            new Position(new AssetSymbol("PETR4"), new Quantity(1), new Money(100), new Percentage(100)));
        var service = CreateService(portfolio, [], 10m);

        var exception = await Assert.ThrowsAsync<PortfolioDataIncompleteException>(() => service.AnalyzeAsync(portfolio.Id));

        Assert.Contains("PETR4", exception.Message);
    }

    private static RiskAnalysisAppService CreateService(Portfolio? portfolio, IEnumerable<Asset> assets, decimal? selicRate) =>
        new(
            new PortfolioRepositoryStub(portfolio),
            new AssetRepositoryStub(assets),
            new MarketDataReaderStub(selicRate),
            new PortfolioRiskCalculator(),
            NullLogger<RiskAnalysisAppService>.Instance);

    private static Portfolio PortfolioWith(decimal investment, params Position[] positions)
    {
        var portfolio = new Portfolio("Test", "user", new Money(investment), new DateTime(2024, 1, 1), positions);
        portfolio.AssignId(1);
        return portfolio;
    }

    private static Asset AssetWith(string symbol, string sector, decimal currentPrice, IEnumerable<decimal>? history = null)
    {
        var asset = new Asset(new AssetSymbol(symbol), symbol, "Stock", sector, new Money(currentPrice), new DateTime(2025, 1, 1));
        if (history is not null)
        {
            asset.SetPriceHistory(history.Select((price, index) => new PricePoint(new DateTime(2024, 1, 1).AddDays(index), new Money(price))));
        }

        return asset;
    }

    private sealed class MarketDataReaderStub(decimal? selicRate) : IMarketDataReader
    {
        public Task<decimal?> GetSelicRateAsync(CancellationToken ct = default) => Task.FromResult(selicRate);
    }

    private sealed class PortfolioRepositoryStub(Portfolio? portfolio) : IPortfolioPositionsReader
    {
        public Task<Portfolio?> GetWithPositionsAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(portfolio is not null && id == portfolio.Id ? portfolio : null);

        public Task<Portfolio?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Portfolio>> GetAllAsync(Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> QueryAsync(Expression<Func<Portfolio, bool>> predicate, Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Portfolio?> QuerySingleAsync(Expression<Func<Portfolio, bool>> predicate, Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Portfolio entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Portfolio entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class AssetRepositoryStub(IEnumerable<Asset> assets) : IAssetPriceHistoryReader
    {
        private readonly IReadOnlyDictionary<AssetSymbol, Asset> _assets = assets.ToDictionary(asset => asset.Symbol);

        public Task<Asset?> GetWithPriceHistoryAsync(AssetSymbol symbol, CancellationToken ct = default) =>
            Task.FromResult(_assets.GetValueOrDefault(symbol));

        public Task<Asset?> GetByIdAsync(AssetSymbol id, CancellationToken ct = default) => Task.FromResult(_assets.GetValueOrDefault(id));
        public Task<IReadOnlyList<Asset>> GetAllAsync(Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Asset>> QueryAsync(Expression<Func<Asset, bool>> predicate, Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Asset?> QuerySingleAsync(Expression<Func<Asset, bool>> predicate, Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Asset entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Asset entity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(AssetSymbol id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }
}
