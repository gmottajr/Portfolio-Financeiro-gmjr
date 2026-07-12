using System.Linq.Expressions;
using Abstractions._02_Application.Services;
using Abstractions._03_Infra.Persistence;
using Abstractions._04_Domain;
using DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace DAL.Repositories.Abstractions;

/// <summary>
/// Base repository implementation using EF Core for aggregate roots.
/// </summary>
/// <typeparam name="TEntity">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The aggregate root key type.</typeparam>
public class EfDataRepositoryBase<TEntity, TKey> : DataRepositoryBase<TEntity, TKey>
    where TEntity : AggregateRoot<TKey>
    where TKey : notnull
{
    protected readonly PortfolioDbContext Context;
    protected readonly DbSet<TEntity> DbSet;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public EfDataRepositoryBase(
        PortfolioDbContext context,
        IDomainEventDispatcher eventDispatcher)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
        _eventDispatcher = eventDispatcher;
    }

    /// <inheritdoc />
    public override async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync([id], ct);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<TEntity>> GetAllAsync(
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = ApplyIncludes(DbSet, includes);

        if (orderBy is not null)
        {
            query = orderBy(query);
        }

        return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = ApplyIncludes(DbSet.Where(predicate), includes);

        if (orderBy is not null)
        {
            query = orderBy(query);
        }

        return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public override async Task<TEntity?> QuerySingleAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Expression<Func<TEntity, object>>[]? includes = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = ApplyIncludes(DbSet.Where(predicate), includes);

        if (orderBy is not null)
        {
            query = orderBy(query);
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public override async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
    }

    /// <inheritdoc />
    public override Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(TKey id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is not null)
        {
            DbSet.Remove(entity);
        }
    }

    /// <inheritdoc />
    public override async Task SaveChangesAsync(CancellationToken ct = default)
    {
        var aggregatesWithEvents = Context.ChangeTracker
            .Entries<EntityBase>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        await Context.SaveChangesAsync(ct);
        await _eventDispatcher.DispatchAsync(domainEvents, ct);

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Applies include expressions to a query for eager loading.
    /// </summary>
    protected static IQueryable<TEntity> ApplyIncludes(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, object>>[]? includes)
    {
        if (includes is null)
        {
            return query;
        }

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return query;
    }
}
