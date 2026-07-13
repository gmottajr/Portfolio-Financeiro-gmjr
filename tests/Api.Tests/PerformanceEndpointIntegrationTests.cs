using System.Net;
using System.Net.Http.Json;
using Application.Performance;
using Application.Risk;
using Application.Rebalancing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed class PerformanceEndpointIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PerformanceEndpointIntegrationTests(WebApplicationFactory<Program> factory) =>
        _client = factory
            .WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.ClearProviders()))
            .CreateClient();

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
    public async Task GetRiskAnalysis_ReturnsAnalysisForSeededPortfolio()
    {
        var response = await _client.GetAsync("/api/portfolios/1/risk-analysis");

        response.EnsureSuccessStatusCode();
        var analysis = await response.Content.ReadFromJsonAsync<RiskAnalysisResponse>();
        Assert.NotNull(analysis);
        Assert.NotNull(analysis.SharpeRatio);
        Assert.NotNull(analysis.ConcentrationRisk.LargestPosition);
        Assert.NotEmpty(analysis.SectorDiversification);
        Assert.NotNull(analysis.Recommendations);
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
    }
}
