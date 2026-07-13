using Models;

namespace Application.Contracts;

public interface IPortfolioPositionsReader
{
    Task<Portfolio?> GetWithPositionsAsync(int id, CancellationToken ct = default);
}

public interface IPortfolioSeedRepository
{
    Task<IReadOnlyList<Portfolio>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(Portfolio entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
