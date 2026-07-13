using Models;
using SharedKernel.ValueObjects;

namespace Application.Contracts;

public interface IAssetReader
{
    Task<Asset?> GetByIdAsync(AssetSymbol symbol, CancellationToken ct = default);
}

public interface IAssetPriceHistoryReader
{
    Task<Asset?> GetWithPriceHistoryAsync(AssetSymbol symbol, CancellationToken ct = default);
}

public interface IAssetSeedRepository : IAssetReader
{
    Task AddAsync(Asset entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
