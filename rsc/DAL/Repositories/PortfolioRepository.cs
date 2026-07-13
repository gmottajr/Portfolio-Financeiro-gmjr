using Abstractions._02_Application.Services;
using Application.Contracts;
using DAL.Data;
using DAL.Repositories.Abstractions;
using Microsoft.EntityFrameworkCore;
using Models;

namespace DAL.Repositories;

/// <summary>
/// EF Core repository for portfolio aggregates.
/// </summary>
public sealed class PortfolioRepository(
    PortfolioDbContext context,
    IDomainEventDispatcher eventDispatcher)
    : EfDataRepositoryBase<Portfolio, int>(context, eventDispatcher), IPortfolioPositionsReader, IPortfolioSeedRepository
{
    /// <inheritdoc />
    public Task<Portfolio?> GetWithPositionsAsync(int id, CancellationToken ct = default)
    {
        return DbSet
            .Include(portfolio => portfolio.Positions)
            .FirstOrDefaultAsync(portfolio => portfolio.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Portfolio>> GetByUserIdAsync(
        string userId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return await DbSet
            .Include(portfolio => portfolio.Positions)
            .Where(portfolio => portfolio.UserId == userId)
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<Portfolio>> GetAllAsync(CancellationToken ct = default) =>
        DbSet.ToListAsync(ct).ContinueWith(task => (IReadOnlyList<Portfolio>)task.Result, ct);
}
