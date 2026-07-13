using DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Models;
using SharedKernel.Exceptions;
using SharedKernel.ValueObjects;

namespace Persistence.Tests;

public sealed class PortfolioDbContextIntegrationTests
{
    [Fact]
    public async Task SaveChangesAsync_PersistsPortfolioAndOwnedPositions()
    {
        await using var context = CreateContext();
        var createdAt = new DateTime(2024, 1, 15, 9, 0, 0, DateTimeKind.Utc);
        var portfolio = new Portfolio(
            "Portfolio de teste",
            "user-001",
            new Money(100_000m),
            createdAt,
            [
                new Position(new AssetSymbol("PETR4"), new Quantity(500), new Money(30m), new Percentage(40m)),
                new Position(new AssetSymbol("VALE3"), new Quantity(300), new Money(60m), new Percentage(60m))
            ]);
        portfolio.AssignId(1);

        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Portfolios
            .Include(x => x.Positions)
            .SingleAsync(x => x.Id == 1);

        Assert.Equal("Portfolio de teste", persisted.Name);
        Assert.Equal("user-001", persisted.UserId);
        Assert.Equal(100_000m, persisted.TotalInvestment.Value);
        Assert.Equal(createdAt, persisted.PortfolioCreatedAt);
        Assert.Collection(
            persisted.Positions.OrderBy(x => x.AssetSymbol.Value),
            petr4 =>
            {
                Assert.Equal("PETR4", petr4.AssetSymbol.Value);
                Assert.Equal(500m, petr4.Quantity.Value);
                Assert.Equal(30m, petr4.AveragePrice.Value);
                Assert.Equal(40m, petr4.TargetAllocation.Value);
            },
            vale3 =>
            {
                Assert.Equal("VALE3", vale3.AssetSymbol.Value);
                Assert.Equal(300m, vale3.Quantity.Value);
                Assert.Equal(60m, vale3.AveragePrice.Value);
                Assert.Equal(60m, vale3.TargetAllocation.Value);
            });
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsAssetAndOwnedPriceHistory()
    {
        await using var context = CreateContext();
        var updatedAt = new DateTime(2024, 10, 6, 10, 30, 0, DateTimeKind.Utc);
        var asset = new Asset(
            new AssetSymbol("PETR4"),
            "Petrobras PN",
            "Stock",
            "Energy",
            new Money(35.50m),
            updatedAt);
        asset.SetPriceHistory(
        [
            new PricePoint(new DateTime(2024, 10, 6, 9, 0, 0), new Money(35.00m)),
            new PricePoint(new DateTime(2024, 10, 6, 16, 0, 0), new Money(35.50m)),
            new PricePoint(new DateTime(2024, 10, 4), new Money(35.25m))
        ]);

        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Assets
            .Include(x => x.PriceHistory)
            .SingleAsync(x => x.Id == new AssetSymbol("PETR4"));

        Assert.Equal("Petrobras PN", persisted.Name);
        Assert.Equal(35.50m, persisted.CurrentPrice.Value);
        Assert.Equal(updatedAt, persisted.LastUpdated);
        Assert.Collection(
            persisted.PriceHistory,
            first =>
            {
                Assert.Equal(new DateTime(2024, 10, 4), first.Date);
                Assert.Equal(35.25m, first.Price.Value);
            },
            second =>
            {
                Assert.Equal(new DateTime(2024, 10, 6, 16, 0, 0), second.Date);
                Assert.Equal(35.50m, second.Price.Value);
            });
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsNullOptionalDates()
    {
        await using var context = CreateContext();
        var portfolio = new Portfolio(
            "Portfolio sem datas",
            "user-002",
            new Money(10_000m),
            portfolioCreatedAt: null,
            [new Position(new AssetSymbol("ITUB4"), new Quantity(100), new Money(30m), new Percentage(100m))]);
        portfolio.AssignId(2);

        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var persisted = await context.Portfolios
            .Include(x => x.Positions)
            .SingleAsync(x => x.Id == 2);

        Assert.Null(persisted.PortfolioCreatedAt);
        Assert.Single(persisted.Positions);
        Assert.Null(persisted.Positions[0].LastTransaction);
    }

    [Fact]
    public void CreatingPortfolioWithDuplicateAssetPositions_ThrowsBeforePersistence()
    {
        var duplicatePositions = new[]
        {
            new Position(new AssetSymbol("PETR4"), new Quantity(100), new Money(30m), new Percentage(50m)),
            new Position(new AssetSymbol("PETR4"), new Quantity(100), new Money(30m), new Percentage(50m))
        };

        var exception = Assert.Throws<DomainException>(() => new Portfolio(
            "Portfolio inválido",
            "user-003",
            new Money(6_000m),
            portfolioCreatedAt: null,
            duplicatePositions));

        Assert.Contains("duplicated positions", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatingPortfolioWithTargetAllocationsThatDoNotTotalOneHundred_ThrowsBeforePersistence()
    {
        var positions = new[]
        {
            new Position(new AssetSymbol("PETR4"), new Quantity(100), new Money(30m), new Percentage(40m)),
            new Position(new AssetSymbol("VALE3"), new Quantity(100), new Money(60m), new Percentage(50m))
        };

        var exception = Assert.Throws<BusinessViolationException>(() => new Portfolio(
            "Portfolio inválido",
            "user-003",
            new Money(6_000m),
            portfolioCreatedAt: null,
            positions));

        Assert.Contains("total 100%", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangeTargetAllocation_RejectsChangeThatWouldInvalidatePortfolioTotal()
    {
        var portfolio = new Portfolio(
            "Portfolio válido",
            "user-003",
            new Money(6_000m),
            portfolioCreatedAt: null,
            [
                new Position(new AssetSymbol("PETR4"), new Quantity(100), new Money(30m), new Percentage(50m)),
                new Position(new AssetSymbol("VALE3"), new Quantity(100), new Money(60m), new Percentage(50m))
            ]);

        var exception = Assert.Throws<BusinessViolationException>(() =>
            portfolio.ChangeTargetAllocation(new AssetSymbol("PETR4"), new Percentage(60m)));

        Assert.Contains("total 100%", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(50m, portfolio.FindPosition(new AssetSymbol("PETR4"))!.TargetAllocation.Value);
    }

    [Fact]
    public void CreatingPositionWithTargetOutsideAllowedRange_ThrowsBusinessViolationException()
    {
        Assert.Throws<BusinessViolationException>(() =>
            new Position(new AssetSymbol("PETR4"), new Quantity(100), new Money(30m), new Percentage(101m)));
    }

    private static PortfolioDbContext CreateContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        var integrationDatabaseName = configuration["Database:InMemory:IntegrationTestName"]
            ?? throw new InvalidOperationException("Integration test database name is not configured.");
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseInMemoryDatabase($"{integrationDatabaseName}-{Guid.NewGuid():N}")
            .Options;

        return new PortfolioDbContext(options);
    }
}
