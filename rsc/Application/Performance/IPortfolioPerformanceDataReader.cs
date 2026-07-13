using Models;
using SharedKernel.ValueObjects;

namespace Application.Performance;

/// <summary>Read model gateway required by the performance query.</summary>
public interface IPortfolioPerformanceDataReader
{
    Task<Portfolio?> GetPortfolioAsync(int portfolioId, CancellationToken ct = default);

    Task<Asset?> GetAssetAsync(AssetSymbol symbol, CancellationToken ct = default);
}
