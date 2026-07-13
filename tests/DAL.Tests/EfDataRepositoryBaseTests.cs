using Abstractions._02_Application.Services;
using Abstractions._04_Domain;
using DAL.Data;
using DAL.Repositories;
using Microsoft.EntityFrameworkCore;
using Models;
using SharedKernel.ValueObjects;

namespace Persistence.Tests;

public sealed class EfDataRepositoryBaseTests
{
    [Fact]
    public async Task QueryMethods_FilterOrderAndReturnSingleEntity()
    {
        await using var context = CreateContext();
        var repository = new AssetRepository(context, new NoOpDomainEventDispatcher());
        await repository.AddAsync(Asset("PETR4", "Energy"));
        await repository.AddAsync(Asset("VALE3", "Mining"));
        await repository.AddAsync(Asset("ITUB4", "Financial"));
        await repository.SaveChangesAsync();

        var filtered = await repository.QueryAsync(
            asset => asset.Sector == "Energy" || asset.Sector == "Mining",
            query => query.OrderByDescending(asset => asset.Name));
        var single = await repository.QuerySingleAsync(asset => asset.Name == "ITUB4");

        Assert.Equal(["VALE3", "PETR4"], filtered.Select(asset => asset.Symbol.Value));
        Assert.NotNull(single);
        Assert.Equal("Financial", single.Sector);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExistingEntityAndIgnoresUnknownId()
    {
        await using var context = CreateContext();
        var repository = new AssetRepository(context, new NoOpDomainEventDispatcher());
        await repository.AddAsync(Asset("PETR4", "Energy"));
        await repository.SaveChangesAsync();

        await repository.DeleteAsync(new AssetSymbol("PETR4"));
        await repository.DeleteAsync(new AssetSymbol("VALE3"));
        await repository.SaveChangesAsync();

        Assert.Null(await repository.GetByIdAsync(new AssetSymbol("PETR4")));
    }

    private static Asset Asset(string symbol, string sector) =>
        new(new AssetSymbol(symbol), symbol, "Stock", sector, new Money(10m), new DateTime(2024, 1, 1));

    private static PortfolioDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default) => Task.CompletedTask;
    }
}
