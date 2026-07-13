using Abstractions._02_Application.Services;
using Abstractions._04_Domain;
using Application.Contracts;
using DAL.Data;
using DAL.Repositories;
using DAL.Sower;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Models;
using SharedKernel.ValueObjects;

namespace IoC.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPortfolioPersistence_UsesIntegrationDatabaseConfigurationForTestingEnvironment()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:InMemory:ProductionName"] = "production-database",
                ["Database:InMemory:IntegrationTestName"] = "integration-database"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddPortfolioPersistence(configuration, new TestHostEnvironment("Testing"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", scope.ServiceProvider.GetRequiredService<PortfolioDbContext>().Database.ProviderName);
    }
    [Fact]
    public void AddPortfolioPersistence_RegistersScopedInMemoryDbContext()
    {
        var services = new ServiceCollection();
        services.AddPortfolioPersistence();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var firstContext = firstScope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<PortfolioDbContext>();

        Assert.NotSame(firstContext, secondContext);
        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", firstContext.Database.ProviderName);
    }

    [Fact]
    public async Task AddPortfolioPersistence_UsesProvidedDatabaseNameAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddPortfolioPersistence("integration-tests");
        using var provider = services.BuildServiceProvider();
        using var writeScope = provider.CreateScope();
        using var readScope = provider.CreateScope();

        var writeContext = writeScope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var lastUpdated = new DateTime(2024, 10, 6, 10, 30, 0, DateTimeKind.Utc);
        var savedAsset = new Asset(
            new AssetSymbol("PETR4"),
            "Petrobras PN",
            "Stock",
            "Energy",
            new Money(35.50m),
            lastUpdated);
        writeContext.Assets.Add(savedAsset);
        await writeContext.SaveChangesAsync();

        var readContext = readScope.ServiceProvider.GetRequiredService<PortfolioDbContext>();
        var persistedAsset = Assert.Single(readContext.Assets);

        Assert.NotSame(writeContext, readContext);
        Assert.NotSame(savedAsset, persistedAsset);
        Assert.Equal("PETR4", persistedAsset.Symbol.Value);
        Assert.Equal("Petrobras PN", persistedAsset.Name);
        Assert.Equal("Stock", persistedAsset.Type);
        Assert.Equal("Energy", persistedAsset.Sector);
        Assert.Equal(35.50m, persistedAsset.CurrentPrice.Value);
        Assert.Equal(lastUpdated, persistedAsset.LastUpdated);
        Assert.Empty(persistedAsset.PriceHistory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPortfolioPersistence_WithBlankDatabaseName_ThrowsArgumentException(string databaseName)
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddPortfolioPersistence(databaseName));
    }

    [Fact]
    public async Task AddPortfolioPersistence_RegistersDomainEventDispatcher()
    {
        var services = new ServiceCollection();
        var handler = new TestDomainEventHandler();
        services.AddSingleton<IDomainEventHandler<Models.Events.PortfolioCreated>>(handler);
        services.AddPortfolioPersistence();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        var domainEvent = new Models.Events.PortfolioCreated(1, "user");

        await dispatcher.DispatchAsync([domainEvent]);

        Assert.Same(domainEvent, handler.HandledEvent);
    }

    [Fact]
    public void AddPortfolioPersistence_RegistersAggregateRepositories()
    {
        var services = new ServiceCollection();
        services.AddPortfolioPersistence();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<AssetRepository>(scope.ServiceProvider.GetRequiredService<IAssetReader>());
        Assert.IsType<AssetRepository>(scope.ServiceProvider.GetRequiredService<IAssetPriceHistoryReader>());
        Assert.IsType<PortfolioRepository>(scope.ServiceProvider.GetRequiredService<IPortfolioPositionsReader>());
        Assert.IsType<DataSower>(scope.ServiceProvider.GetRequiredService<IDataSower>());
    }

    private sealed class TestDomainEventHandler : IDomainEventHandler<Models.Events.PortfolioCreated>
    {
        public Models.Events.PortfolioCreated? HandledEvent { get; private set; }

        public Task HandleAsync(Models.Events.PortfolioCreated domainEvent, CancellationToken ct = default)
        {
            HandledEvent = domainEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "IoC.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
