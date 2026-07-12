using System.Linq.Expressions;
using Abstractions._04_Domain;

namespace Abstractions._03_Infra.Persistence;

/// <summary>
/// Abstract base class for repositories providing shared contract definitions.
/// </summary>
/// <typeparam name="TEntity">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The aggregate root key type.</typeparam>
public abstract class DataRepositoryBase<TEntity, TKey> : IDataRepositoryBase<TEntity, TKey>
    where TEntity : AggregateRoot<TKey>
    where TKey : notnull
{
    /// <inheritdoc />
    public abstract Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<IReadOnlyList<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task<TEntity?> QuerySingleAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task AddAsync(TEntity entity, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task UpdateAsync(TEntity entity, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task DeleteAsync(TKey id, CancellationToken ct = default);

    /// <inheritdoc />
    public abstract Task SaveChangesAsync(CancellationToken ct = default);
}
