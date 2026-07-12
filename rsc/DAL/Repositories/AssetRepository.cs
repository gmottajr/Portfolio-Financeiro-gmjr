using Abstractions._02_Application.Services;
using DAL.Data;
using DAL.Repositories.Abstractions;
using DAL.Repositories.Contracts;
using Microsoft.EntityFrameworkCore;
using Models;
using SharedKernel.ValueObjects;

namespace DAL.Repositories;

/// <summary>
/// EF Core repository for asset aggregates.
/// </summary>
public sealed class AssetRepository(
    PortfolioDbContext context,
    IDomainEventDispatcher eventDispatcher)
    : EfDataRepositoryBase<Asset, AssetSymbol>(context, eventDispatcher), IAssetRepository
{
    /// <inheritdoc />
    public Task<Asset?> GetWithPriceHistoryAsync(AssetSymbol symbol, CancellationToken ct = default)
    {
        return DbSet
            .Include(asset => asset.PriceHistory)
            .FirstOrDefaultAsync(asset => asset.Id == symbol, ct);
    }
}
