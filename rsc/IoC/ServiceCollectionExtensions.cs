using Abstractions._02_Application.Services;
using DAL.Data;
using DAL.Repositories;
using DAL.Repositories.Contracts;
using DAL.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
            .AddPortfolioRepositories();
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
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();

        return services;
    }
}
