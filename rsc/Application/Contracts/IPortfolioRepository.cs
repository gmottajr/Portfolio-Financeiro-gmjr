using Abstractions._03_Infra.Persistence;
using Models;

namespace Application.Contracts;

/// <summary>
/// Provides persistence operations for portfolio aggregates.
/// </summary>
public interface IPortfolioRepository : IDataRepositoryBase<Portfolio, int>
{
    /// <summary>
    /// Gets a portfolio and all of its positions.
    /// </summary>
    Task<Portfolio?> GetWithPositionsAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets all portfolios owned by a user, including their positions.
    /// </summary>
    Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(string userId, CancellationToken ct = default);
}
