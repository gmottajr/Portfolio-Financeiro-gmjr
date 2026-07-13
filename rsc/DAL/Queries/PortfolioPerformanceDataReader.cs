using Application.Performance;
using Application.Contracts;
using Models;
using SharedKernel.ValueObjects;

namespace DAL.Queries;

/// <summary>EF Core implementation of the read gateway used by performance analysis.</summary>
public sealed class PortfolioPerformanceDataReader(
    IPortfolioRepository portfolioRepository,
    IAssetRepository assetRepository) : IPortfolioPerformanceDataReader
{
    public Task<Portfolio?> GetPortfolioAsync(int portfolioId, CancellationToken ct = default) =>
        portfolioRepository.GetWithPositionsAsync(portfolioId, ct);

    public Task<Asset?> GetAssetAsync(AssetSymbol symbol, CancellationToken ct = default) =>
        assetRepository.GetWithPriceHistoryAsync(symbol, ct);
}
