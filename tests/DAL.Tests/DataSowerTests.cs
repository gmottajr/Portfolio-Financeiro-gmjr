using Abstractions._02_Application.Services;
using Abstractions._04_Domain;
using DAL.Data;
using DAL.Repositories;
using DAL.Sower;
using Microsoft.EntityFrameworkCore;
using SharedKernel.ValueObjects;

namespace Persistence.Tests;

public sealed class DataSowerTests
{
    [Fact]
    public async Task SowAsync_LoadsAssetsPortfoliosAndTheirNestedDataFromSeedFile()
    {
        await using var context = CreateContext();
        var sower = CreateSower(context);

        await sower.SowAsync(FindSeedDataPath());
        context.ChangeTracker.Clear();

        Assert.Equal(15, await context.Assets.CountAsync());
        Assert.Equal(3, await context.Portfolios.CountAsync());
        var marketData = await context.MarketDataSnapshots
            .Include(snapshot => snapshot.Indexes)
            .Include(snapshot => snapshot.Sectors)
            .ThenInclude(sector => sector.Assets)
            .SingleAsync();
        Assert.Equal(0.12m, marketData.SelicRate);
        Assert.Single(marketData.Indexes, index => index.Code == "IBOV" && index.CurrentValue == 130245.67m);
        Assert.Equal(6, marketData.Sectors.Count);
        Assert.Contains(marketData.Sectors, sector => sector.Name == "Financial" && sector.Assets.Count == 3);
        Assert.Equal(3, await context.SeedTestScenarios.CountAsync());
        Assert.Contains("Portfolio Desbalanceado", await context.SeedTestScenarios.Select(x => x.Name).ToListAsync());

        var petr4 = await context.Assets
            .Include(asset => asset.PriceHistory)
            .SingleAsync(asset => asset.Id == new AssetSymbol("PETR4"));
        Assert.Equal("Petrobras PN", petr4.Name);
        Assert.Equal(35.50m, petr4.CurrentPrice.Value);
        Assert.Equal(new DateTime(2024, 10, 6, 10, 30, 0, DateTimeKind.Utc), petr4.LastUpdated);
        Assert.Equal(30, petr4.PriceHistory.Count);
        Assert.Contains(petr4.PriceHistory, point =>
            point.Date == new DateTime(2024, 9, 6) && point.Price.Value == 32.10m);

        var conservativePortfolio = await context.Portfolios
            .Include(portfolio => portfolio.Positions)
            .SingleAsync(portfolio => portfolio.UserId == "user-001");
        Assert.Equal("Portfólio Conservador", conservativePortfolio.Name);
        Assert.Equal(100_000m, conservativePortfolio.TotalInvestment.Value);
        Assert.Equal(5, conservativePortfolio.Positions.Count);
        var petr4Position = Assert.Single(conservativePortfolio.Positions,
            position => position.AssetSymbol == new AssetSymbol("PETR4"));
        Assert.Equal(500m, petr4Position.Quantity.Value);
        Assert.Equal(30m, petr4Position.AveragePrice.Value);
        Assert.Equal(20m, petr4Position.TargetAllocation.Value);
    }

    [Fact]
    public async Task SowAsync_IsIdempotentWhenTheSameSeedFileIsLoadedMoreThanOnce()
    {
        await using var context = CreateContext();
        var sower = CreateSower(context);

        var seedDataPath = FindSeedDataPath();
        await sower.SowAsync(seedDataPath);
        await sower.SowAsync(seedDataPath);
        context.ChangeTracker.Clear();

        Assert.Equal(15, await context.Assets.CountAsync());
        Assert.Equal(3, await context.Portfolios.CountAsync());
        Assert.Single(await context.MarketDataSnapshots.ToListAsync());
        Assert.Equal(3, await context.SeedTestScenarios.CountAsync());
        Assert.Equal(15, await context.Portfolios
            .SelectMany(portfolio => portfolio.Positions)
            .CountAsync());
    }

    [Fact]
    public async Task SowAsync_LoadsSeedFileCopiedToApplicationOutput()
    {
        await using var context = CreateContext();
        var sower = CreateSower(context);

        await sower.SowAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(15, await context.Assets.CountAsync());
        Assert.Equal(3, await context.Portfolios.CountAsync());
    }

    private static DataSower CreateSower(PortfolioDbContext context)
    {
        var dispatcher = new NoOpDomainEventDispatcher();
        return new DataSower(
            new AssetRepository(context, dispatcher),
            new PortfolioRepository(context, dispatcher),
            context);
    }

    private static PortfolioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PortfolioDbContext(options);
    }

    private static string FindSeedDataPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var seedDataPath = Path.Combine(directory.FullName, "Data", "SeedData.json");
            if (File.Exists(seedDataPath))
            {
                return seedDataPath;
            }
        }

        throw new FileNotFoundException("SeedData.json was not found.");
    }

    private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
