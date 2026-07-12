using Abstractions._02_Application.Services;
using DAL.Data;
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

        services.AddLogging();
        services.AddDbContext<PortfolioDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
