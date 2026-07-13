using Abstractions._02_Application.Services;
using Application.Contracts;
using Application.Performance;
using Application.Performance.Queries;
using Application.Performance.Services;
using Application.Risk;
using Application.Rebalancing;
using DAL.Data;
using DAL.Queries;
using DAL.Repositories;
using DAL.Services;
using DAL.Sower;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IoC;

/// <summary>Registros de infraestrutura expostos para a camada de composição.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra a persistência local usada pela aplicação. O provider e o
    /// DbContext continuam encapsulados nas camadas de infraestrutura/IoC.
    /// </summary>
    public static IServiceCollection AddPortfolioPersistence(
        this IServiceCollection services,
        string databaseName = "portfolio-analytics")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        return services
            .AddPortfolioLogging()
            .AddPortfolioDbContext(databaseName)
            .AddDomainEventDispatching()
            .AddPortfolioRepositories()
            .AddPortfolioDataSower();
    }

    /// <summary>
    /// Registers the logging abstractions required by infrastructure services.
    /// </summary>
    public static IServiceCollection AddPortfolioLogging(this IServiceCollection services)
    {
        services.AddLogging();
        return services;
    }

    /// <summary>
    /// Registers the portfolio EF Core context backed by the in-memory provider.
    /// </summary>
    public static IServiceCollection AddPortfolioDbContext(
        this IServiceCollection services,
        string databaseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);

        services.AddDbContext<PortfolioDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        return services;
    }

    /// <summary>
    /// Registers the scoped dispatcher used to publish domain events.
    /// </summary>
    public static IServiceCollection AddDomainEventDispatching(this IServiceCollection services)
    {
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }

    /// <summary>
    /// Registers repositories for all portfolio aggregate roots.
    /// </summary>
    public static IServiceCollection AddPortfolioRepositories(this IServiceCollection services)
    {
        services.AddScoped<AssetRepository>();
        services.AddScoped<IAssetReader>(provider => provider.GetRequiredService<AssetRepository>());
        services.AddScoped<IAssetPriceHistoryReader>(provider => provider.GetRequiredService<AssetRepository>());
        services.AddScoped<IAssetSeedRepository>(provider => provider.GetRequiredService<AssetRepository>());
        services.AddScoped<PortfolioRepository>();
        services.AddScoped<IPortfolioPositionsReader>(provider => provider.GetRequiredService<PortfolioRepository>());
        services.AddScoped<IPortfolioSeedRepository>(provider => provider.GetRequiredService<PortfolioRepository>());
        services.AddScoped<IPortfolioPerformanceDataReader, PortfolioPerformanceDataReader>();
        services.AddScoped<IMarketDataReader, MarketDataReader>();

        return services;
    }

    /// <summary>
    /// Registers the scoped service responsible for loading initial data.
    /// </summary>
    public static IServiceCollection AddPortfolioDataSower(this IServiceCollection services)
    {
        services.AddScoped<IDataSower, DataSower>();

        return services;
    }

    public static IServiceCollection AddPortfolioPerformanceAnalysis(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<GetPortfolioPerformanceQueryHandler>());
        services.AddScoped<IPerformanceCalculator, PerformanceCalculator>();
        services.AddScoped<PortfolioRiskCalculator>();
        services.AddScoped<RiskAnalysisAppService>();
        services.AddScoped<IRiskAnalysisAppService>(provider => provider.GetRequiredService<RiskAnalysisAppService>());
        services.AddSingleton<IRebalancingOptimizer, RebalancingOptimizer>();
        services.AddScoped<GenerateRebalancingSuggestionsUseCase>();
        services.AddScoped<IGenerateRebalancingSuggestionsUseCase>(provider => provider.GetRequiredService<GenerateRebalancingSuggestionsUseCase>());

        return services;
    }
}
