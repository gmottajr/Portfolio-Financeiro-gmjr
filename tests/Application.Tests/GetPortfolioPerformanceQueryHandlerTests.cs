using Application.Exceptions;
using Application.Performance;
using Application.Performance.Queries;
using Application.Performance.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using SharedKernel.ValueObjects;

namespace Application.Tests;

public sealed class GetPortfolioPerformanceQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenPortfolioHasNoPositions_ReportsIncompleteData()
    {
        var portfolio = new Portfolio(
            "Empty",
            "user",
            new Money(100m),
            new DateTime(2024, 1, 1),
            []);
        portfolio.AssignId(1);
        var handler = new GetPortfolioPerformanceQueryHandler(
            new PortfolioPerformanceDataReaderStub(portfolio),
            new PerformanceCalculator(),
            NullLogger<GetPortfolioPerformanceQueryHandler>.Instance);

        var exception = await Assert.ThrowsAsync<PortfolioDataIncompleteException>(() =>
            handler.Handle(new GetPortfolioPerformanceQuery(portfolio.Id), CancellationToken.None));

        Assert.Equal("Portfolio 1 has no positions.", exception.Message);
    }

    private sealed class PortfolioPerformanceDataReaderStub(Portfolio portfolio)
        : IPortfolioPerformanceDataReader
    {
        public Task<Portfolio?> GetPortfolioAsync(
            int portfolioId,
            CancellationToken ct = default) =>
            Task.FromResult(portfolioId == portfolio.Id ? portfolio : null);

        public Task<Asset?> GetAssetAsync(
            AssetSymbol symbol,
            CancellationToken ct = default) =>
            Task.FromResult<Asset?>(null);
    }
}
