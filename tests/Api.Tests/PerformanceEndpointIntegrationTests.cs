using System.Net;
using System.Net.Http.Json;
using System.Linq.Expressions;
using System.Globalization;
using System.Text.Json;
using Application.Contracts;
using Application.Performance;
using Application.Risk;
using Application.Rebalancing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Models;
using SharedKernel.Enums;
using SharedKernel.ValueObjects;
using Swashbuckle.AspNetCore.Swagger;

namespace Api.Tests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
                optional: false,
                reloadOnChange: false));
    }
}

public sealed class PerformanceEndpointIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public static IEnumerable<object[]> RebalancingAccuracyCases =>
    [
        [
            "exhaustive", "Exhaustive", 0.1779m, 17.6269m, 87.25m,
            "TOTS3:SELL:381.2058:11207.4505:33.62|WEGE3:BUY:167.6208:7182.5513:21.55|MGLU3:BUY:819.3614:7169.4123:21.51|RENT3:SELL:63.1597:3524.3113:10.57"
        ],
        [
            "quadraticProgramming", "QuadraticProgramming", 2.4840m, 15.3277m, 75.87m,
            "TOTS3:SELL:331.5062:9746.2823:29.24|WEGE3:BUY:145.7673:6246.1288:18.74|MGLU3:BUY:712.5375:6234.7031:18.70|RENT3:SELL:54.9253:3064.8317:9.19"
        ],
        [
            "cpSat", "CpSat", 0.2203m, 17.5845m, 87.23m,
            "TOTS3:SELL:380.6122:11189.9987:33.57|WEGE3:BUY:168.0280:7199.9998:21.60|MGLU3:BUY:820.8571:7182.4996:21.55|RENT3:SELL:62.7688:3502.4990:10.51"
        ]
    ];

    public PerformanceEndpointIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();
    }

    [Fact]
    public void Factory_UsesTestingEnvironmentAndSharedIntegrationDatabaseConfiguration()
    {
        var environment = _factory.Services.GetRequiredService<IHostEnvironment>();
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("Testing", environment.EnvironmentName);
        Assert.Equal("portfolio-analytics-integration-tests", configuration["Database:InMemory:IntegrationTestName"]);
        Assert.NotEqual(configuration["Database:InMemory:ProductionName"], configuration["Database:InMemory:IntegrationTestName"]);
    }

    [Fact]
    public async Task GetPerformance_ReturnsCalculatedPerformanceForSeededPortfolio()
    {
        var response = await _client.GetAsync("/api/portfolios/1/performance");

        response.EnsureSuccessStatusCode();
        var performance = await response.Content.ReadFromJsonAsync<PortfolioPerformanceResponse>();
        Assert.NotNull(performance);
        Assert.Equal(100_000m, performance.TotalInvestment);
        Assert.Equal(5, performance.PositionsPerformance.Count);
        Assert.NotNull(performance.Volatility);
    }

    [Fact]
    public void OpenApi_PerformanceSuccessResponseContainsSchemaAndRepresentativeExample()
    {
        var swagger = _factory.Services
            .GetRequiredService<ISwaggerProvider>()
            .GetSwagger("v1");
        var operation = swagger.Paths["/api/portfolios/{id}/performance"]
            .Operations[OperationType.Get];
        var jsonResponse = operation.Responses["200"].Content["application/json"];

        Assert.Equal("PortfolioPerformanceResponse", jsonResponse.Schema.Reference.Id);

        var example = Assert.IsType<OpenApiObject>(jsonResponse.Example);
        Assert.Equal(100_000d, Assert.IsType<OpenApiDouble>(example["totalInvestment"]).Value);
        Assert.Equal(-19.06d, Assert.IsType<OpenApiDouble>(example["totalReturn"]).Value);
        Assert.Equal(5, Assert.IsType<OpenApiArray>(example["positionsPerformance"]).Count);

        var riskLevel = swagger.Components.Schemas["RiskLevelEnum"];
        Assert.Equal("string", riskLevel.Type);
        Assert.Equal(
            ["Low", "Medium", "High"],
            riskLevel.Enum.Cast<OpenApiString>().Select(value => value.Value));
        var tradeAction = swagger.Components.Schemas["TradeActionEnum"];
        Assert.Equal("string", tradeAction.Type);
        Assert.Equal(
            ["BUY", "SELL"],
            tradeAction.Enum.Cast<OpenApiString>().Select(value => value.Value));
    }

    [Fact]
    public async Task GetPerformance_ReturnsNotFoundForUnknownPortfolio()
    {
        var response = await _client.GetAsync("/api/portfolios/999/performance");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPerformance_ReturnsBadRequestForNonPositiveId()
    {
        var response = await _client.GetAsync("/api/portfolios/0/performance");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPerformance_ReturnsUnprocessableEntityForPortfolioWithoutPositions()
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IPortfolioPerformanceDataReader>();
                    services.AddScoped<IPortfolioPerformanceDataReader>(_ =>
                        new EmptyPortfolioPerformanceDataReader());
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/portfolios/1/performance");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Portfolio data is incomplete.", problem.Title);
        Assert.Equal("Portfolio 1 has no positions.", problem.Detail);
        Assert.Equal(422, problem.Status);
    }

    [Fact]
    public async Task GetRiskAnalysis_ReturnsAnalysisForSeededPortfolio()
    {
        var response = await _client.GetAsync("/api/portfolios/1/risk-analysis");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"overallRisk\":\"High\"", payload);
        var analysis = JsonSerializer.Deserialize<RiskAnalysisResponse>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(analysis);
        Assert.NotNull(analysis.SharpeRatio);
        Assert.NotNull(analysis.ConcentrationRisk.LargestPosition);
        Assert.NotEmpty(analysis.SectorDiversification);
        Assert.NotNull(analysis.Recommendations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SeededPortfolios_ReturnVolatilityAndSharpeRatio(int portfolioId)
    {
        var performance = await _client.GetFromJsonAsync<PortfolioPerformanceResponse>(
            $"/api/portfolios/{portfolioId}/performance");
        var risk = await _client.GetFromJsonAsync<RiskAnalysisResponse>(
            $"/api/portfolios/{portfolioId}/risk-analysis");

        Assert.NotNull(performance?.Volatility);
        Assert.NotNull(risk?.SharpeRatio);
    }

    [Fact]
    public async Task GetRiskAnalysis_UsesAnnualizedReturnAndVolatilityForSharpeRatio()
    {
        var portfolio = new Portfolio(
            "Sharpe",
            "user",
            new Money(100m),
            new DateTime(2024, 1, 1),
            [new Position(new AssetSymbol("PETR4"), new Quantity(1m), new Money(100m), new Percentage(100m))]);
        portfolio.AssignId(1);
        var asset = new Asset(new AssetSymbol("PETR4"), "Petrobras", AssetTypeEnum.Stock, "Energy", new Money(120m), new DateTime(2025, 1, 1));
        asset.SetPriceHistory(
        [
            new PricePoint(new DateTime(2024, 12, 30), new Money(100m)),
            new PricePoint(new DateTime(2024, 12, 31), new Money(110m)),
            new PricePoint(new DateTime(2025, 1, 1), new Money(100m))
        ]);

        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPortfolioPositionsReader>();
                services.RemoveAll<IAssetReader>();
                services.RemoveAll<IAssetPriceHistoryReader>();
                services.RemoveAll<IMarketDataReader>();
                services.AddScoped<IPortfolioPositionsReader>(_ => new PortfolioRepositoryStub(portfolio));
                services.AddScoped<IAssetReader>(_ => new AssetRepositoryStub([asset]));
                services.AddScoped<IAssetPriceHistoryReader>(_ => new AssetRepositoryStub([asset]));
                services.AddScoped<IMarketDataReader>(_ => new MarketDataReaderStub(10m));
            }));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/portfolios/1/risk-analysis");

        response.EnsureSuccessStatusCode();
        var analysis = await response.Content.ReadFromJsonAsync<RiskAnalysisResponse>();
        Assert.NotNull(analysis);
        Assert.Equal(0.0656m, analysis.SharpeRatio);
    }

    [Theory]
    [InlineData("/api/portfolios/0/risk-analysis", HttpStatusCode.BadRequest)]
    [InlineData("/api/portfolios/999/risk-analysis", HttpStatusCode.NotFound)]
    [InlineData("/api/portfolios/0/rebalancing", HttpStatusCode.BadRequest)]
    [InlineData("/api/portfolios/999/rebalancing", HttpStatusCode.NotFound)]
    public async Task AnalyticsEndpoints_ReturnExpectedStatusForInvalidOrUnknownPortfolio(string url, HttpStatusCode expectedStatus)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/portfolios/1/performance")]
    [InlineData("/api/portfolios/1/risk-analysis")]
    [InlineData("/api/portfolios/1/rebalancing")]
    public async Task AnalyticsEndpoints_ReturnUnprocessableEntityForIncompletePortfolioData(string url)
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IPortfolioPositionsReader>();
                    services.RemoveAll<IAssetReader>();
                    services.RemoveAll<IAssetPriceHistoryReader>();
                    services.RemoveAll<IPortfolioPerformanceDataReader>();
                    services.AddScoped<IPortfolioPositionsReader>(_ => new PortfolioRepositoryStub(IncompletePortfolio()));
                    services.AddScoped<IAssetReader>(_ => new AssetRepositoryStub([]));
                    services.AddScoped<IAssetPriceHistoryReader>(_ => new AssetRepositoryStub([]));
                    services.AddScoped<IPortfolioPerformanceDataReader, MissingPerformanceDataReader>();
                });
            });
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Portfolio data is incomplete.", problem.Title);
        Assert.Equal(422, problem.Status);
    }

    [Fact]
    public async Task GetRebalancing_ReturnsSuggestionsForSeededPortfolio()
    {
        var response = await _client.GetAsync("/api/portfolios/1/rebalancing");

        response.EnsureSuccessStatusCode();
        var rebalancing = await response.Content.ReadFromJsonAsync<RebalancingResponse>();
        Assert.NotNull(rebalancing);
        Assert.Equal(5, rebalancing.CurrentAllocation.Count);
        Assert.NotNull(rebalancing.SuggestedTrades);
        Assert.NotNull(rebalancing.ExpectedImprovement);
        Assert.True(rebalancing.TotalTransactionCost >= 0m);
        Assert.NotNull(rebalancing.Optimization);
        Assert.Equal("CompareAll", rebalancing.Optimization.RequestedMode);
        Assert.Equal(3, rebalancing.Optimization.Alternatives.Count);
    }

    [Theory]
    [MemberData(nameof(RebalancingAccuracyCases))]
    public async Task GetRebalancing_ReturnsAccurateDeterministicResultForEachStrategy(
        string mode,
        string expectedStrategy,
        decimal expectedTrackingErrorAfter,
        decimal expectedNetBenefit,
        decimal expectedTotalCost,
        string expectedTradeFingerprint)
    {
        var response = await _client.GetAsync($"/api/portfolios/2/rebalancing?mode={mode}");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RebalancingResponse>();
        Assert.NotNull(result?.Optimization);
        var alternative = Assert.Single(result.Optimization.Alternatives);
        Assert.Equal(expectedStrategy, alternative.Strategy);
        Assert.Equal(expectedStrategy, result.Optimization.SelectedStrategy);
        Assert.Equal("Succeeded", alternative.Status);
        Assert.True(alternative.Metrics.IsFeasible);
        Assert.True(alternative.Metrics.IsSelfFinanced);
        Assert.Equal(17.8578m, alternative.Metrics.TrackingErrorBefore);
        Assert.Equal(expectedTrackingErrorAfter, alternative.Metrics.TrackingErrorAfter);
        Assert.Equal(expectedNetBenefit, alternative.Metrics.NetBenefit);
        Assert.Equal(expectedTotalCost, result.TotalTransactionCost);
        Assert.Equal(4, alternative.Metrics.TradeCount);
        Assert.Equal(alternative.Metrics.TradeCount, alternative.SuggestedTrades.Count);
        Assert.Equal(alternative.SuggestedTrades, result.SuggestedTrades);
        Assert.Equal(
            alternative.Metrics.GrossImprovement,
            alternative.Metrics.TrackingErrorBefore - alternative.Metrics.TrackingErrorAfter);
        Assert.Equal(
            alternative.Metrics.NetBenefit,
            alternative.Metrics.GrossImprovement - alternative.Metrics.CostImpact);
        Assert.Equal(
            result.TotalTransactionCost,
            alternative.SuggestedTrades.Sum(trade => trade.TransactionCost));

        Assert.All(alternative.SuggestedTrades, trade =>
        {
            Assert.True(trade.EstimatedValue >= 100m);
            Assert.True(trade.Quantity > 0m);
            Assert.Equal(
                decimal.Round(trade.EstimatedValue * 0.003m, 2, MidpointRounding.AwayFromZero),
                trade.TransactionCost);
        });
        var netSales = alternative.SuggestedTrades
            .Where(trade => trade.Action == TradeActionEnum.Sell)
            .Sum(trade => trade.EstimatedValue - trade.TransactionCost);
        var grossPurchases = alternative.SuggestedTrades
            .Where(trade => trade.Action == TradeActionEnum.Buy)
            .Sum(trade => trade.EstimatedValue + trade.TransactionCost);
        Assert.True(grossPurchases <= netSales + 0.01m);

        var fingerprint = string.Join(
            "|",
            alternative.SuggestedTrades.Select(trade => string.Join(
                ":",
                trade.Symbol,
                trade.Action.ToString().ToUpperInvariant(),
                trade.Quantity.ToString("F4", CultureInfo.InvariantCulture),
                trade.EstimatedValue.ToString("F4", CultureInfo.InvariantCulture),
                trade.TransactionCost.ToString("F2", CultureInfo.InvariantCulture))));
        Assert.Equal(expectedTradeFingerprint, fingerprint);
    }

    [Fact]
    public async Task GetRebalancing_IsIdempotentForTheSameInputs()
    {
        var first = await _client.GetStringAsync(
            "/api/portfolios/2/rebalancing?mode=exhaustive");
        var second = await _client.GetStringAsync(
            "/api/portfolios/2/rebalancing?mode=exhaustive");

        Assert.Equal(first, second);
        Assert.Contains("\"action\":\"SELL\"", first);
        Assert.Contains("\"action\":\"BUY\"", first);
    }

    [Fact]
    public async Task GetRebalancing_ReturnsBadRequestForUnknownOptimizationMode()
    {
        var response = await _client.GetAsync(
            "/api/portfolios/2/rebalancing?mode=unknown");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyticsEndpoints_ReturnTooManyRequestsWhenRateLimitIsExceeded()
    {
        using var factory = new ApiWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["RateLimiting:Analytics:PermitLimit"] = "2",
                        ["RateLimiting:Analytics:WindowSeconds"] = "60"
                    }));
            });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/portfolios/1/performance")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/portfolios/1/performance")).StatusCode);
        var rejected = await client.GetAsync("/api/portfolios/1/performance");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    private static Portfolio IncompletePortfolio()
    {
        var portfolio = new Portfolio(
            "Incomplete",
            "user",
            new Money(100m),
            new DateTime(2024, 1, 1),
            [new Position(new AssetSymbol("PETR4"), new Quantity(1m), new Money(100m), new Percentage(100m))]);
        portfolio.AssignId(1);
        return portfolio;
    }

    private sealed class MissingPerformanceDataReader : IPortfolioPerformanceDataReader
    {
        public Task<Portfolio?> GetPortfolioAsync(int portfolioId, CancellationToken ct = default) =>
            Task.FromResult(portfolioId == 1 ? IncompletePortfolio() : null);

        public Task<Asset?> GetAssetAsync(AssetSymbol symbol, CancellationToken ct = default) => Task.FromResult<Asset?>(null);
    }

    private sealed class EmptyPortfolioPerformanceDataReader : IPortfolioPerformanceDataReader
    {
        private readonly Portfolio _portfolio = CreatePortfolio();

        public Task<Portfolio?> GetPortfolioAsync(int portfolioId, CancellationToken ct = default) =>
            Task.FromResult(portfolioId == _portfolio.Id ? _portfolio : null);

        public Task<Asset?> GetAssetAsync(AssetSymbol symbol, CancellationToken ct = default) =>
            Task.FromResult<Asset?>(null);

        private static Portfolio CreatePortfolio()
        {
            var portfolio = new Portfolio(
                "Empty",
                "user",
                new Money(100m),
                new DateTime(2024, 1, 1),
                []);
            portfolio.AssignId(1);
            return portfolio;
        }
    }

    private sealed class PortfolioRepositoryStub(Portfolio portfolio) : IPortfolioPositionsReader
    {
        public Task<Portfolio?> GetWithPositionsAsync(int id, CancellationToken ct = default) => Task.FromResult(id == portfolio.Id ? portfolio : null);
        public Task<Portfolio?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Portfolio>> GetAllAsync(Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<IReadOnlyList<Portfolio>> QueryAsync(Expression<Func<Portfolio, bool>> predicate, Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task<Portfolio?> QuerySingleAsync(Expression<Func<Portfolio, bool>> predicate, Func<IQueryable<Portfolio>, IOrderedQueryable<Portfolio>>? orderBy = null, Expression<Func<Portfolio, object>>[]? includes = null, CancellationToken ct = default) => Task.FromResult<Portfolio?>(null);
        public Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(string userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Portfolio>>([]);
        public Task AddAsync(Portfolio entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Portfolio entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class AssetRepositoryStub(IEnumerable<Asset> assets) : IAssetReader, IAssetPriceHistoryReader
    {
        private readonly IReadOnlyDictionary<AssetSymbol, Asset> _assets = assets.ToDictionary(asset => asset.Symbol);

        public Task<Asset?> GetByIdAsync(AssetSymbol id, CancellationToken ct = default) => Task.FromResult(_assets.GetValueOrDefault(id));
        public Task<Asset?> GetWithPriceHistoryAsync(AssetSymbol symbol, CancellationToken ct = default) => Task.FromResult(_assets.GetValueOrDefault(symbol));
        public Task<IReadOnlyList<Asset>> GetAllAsync(Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Asset>>([]);
        public Task<IReadOnlyList<Asset>> QueryAsync(Expression<Func<Asset, bool>> predicate, Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Asset>>([]);
        public Task<Asset?> QuerySingleAsync(Expression<Func<Asset, bool>> predicate, Func<IQueryable<Asset>, IOrderedQueryable<Asset>>? orderBy = null, Expression<Func<Asset, object>>[]? includes = null, CancellationToken ct = default) => Task.FromResult<Asset?>(null);
        public Task AddAsync(Asset entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Asset entity, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(AssetSymbol id, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class MarketDataReaderStub(decimal? selicRate) : IMarketDataReader
    {
        public Task<decimal?> GetSelicRateAsync(CancellationToken ct = default) => Task.FromResult(selicRate);
    }
}
