using System.Linq.Expressions;
using Abstractions._04_Domain;

namespace Abstractions._03_Infra.Persistence;

/// <summary>
/// Defines the common asynchronous operations for data repositories.
/// </summary>
/// <typeparam name="TEntity">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The aggregate root key type.</typeparam>
public interface IDataRepositoryBase<TEntity, TKey>
    where TEntity : AggregateRoot<TKey>
    where TKey : notnull
{
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default);

    Task<TEntity?> QuerySingleAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default);

    Task AddAsync(TEntity entity, CancellationToken ct = default);

    Task UpdateAsync(TEntity entity, CancellationToken ct = default);

    Task DeleteAsync(TKey id, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
