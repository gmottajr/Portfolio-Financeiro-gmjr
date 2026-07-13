using Abstractions._03_Infra.Persistence;
using Models;
using SharedKernel.ValueObjects;

namespace Application.Contracts;

/// <summary>
/// Provides persistence operations for asset aggregates.
/// </summary>
public interface IAssetRepository : IDataRepositoryBase<Asset, AssetSymbol>
{
    /// <summary>
    /// Gets an asset and its complete price history.
    /// </summary>
    Task<Asset?> GetWithPriceHistoryAsync(AssetSymbol symbol, CancellationToken ct = default);
}
