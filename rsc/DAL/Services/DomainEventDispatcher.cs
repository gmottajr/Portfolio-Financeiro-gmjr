using Abstractions._02_Application.Services;
using Abstractions._04_Domain;
using Microsoft.Extensions.Logging;
using Models.Events;

namespace DAL.Services;

/// <summary>Dispatches the application's known domain events to explicitly injected handlers.</summary>
public sealed class DomainEventDispatcher(
    IEnumerable<IDomainEventHandler<AssetPriceUpdated>> assetPriceUpdatedHandlers,
    IEnumerable<IDomainEventHandler<PortfolioCreated>> portfolioCreatedHandlers,
    IEnumerable<IDomainEventHandler<PositionRebalanced>> positionRebalancedHandlers,
    IEnumerable<IDomainEventHandler<TargetAllocationChanged>> targetAllocationChangedHandlers,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            ct.ThrowIfCancellationRequested();
            switch (domainEvent)
            {
                case AssetPriceUpdated assetPriceUpdated:
                    await DispatchAsync(assetPriceUpdated, assetPriceUpdatedHandlers, ct);
                    break;
                case PortfolioCreated portfolioCreated:
                    await DispatchAsync(portfolioCreated, portfolioCreatedHandlers, ct);
                    break;
                case PositionRebalanced positionRebalanced:
                    await DispatchAsync(positionRebalanced, positionRebalancedHandlers, ct);
                    break;
                case TargetAllocationChanged targetAllocationChanged:
                    await DispatchAsync(targetAllocationChanged, targetAllocationChangedHandlers, ct);
                    break;
                default:
                    logger.LogWarning("No dispatcher route is configured for domain event {EventType}.", domainEvent.GetType().Name);
                    break;
            }
        }
    }

    private async Task DispatchAsync<TEvent>(TEvent domainEvent, IEnumerable<IDomainEventHandler<TEvent>> handlers, CancellationToken ct)
        where TEvent : IDomainEvent
    {
        logger.LogDebug("Dispatching domain event {EventType}", typeof(TEvent).Name);
        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(domainEvent, ct);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Error handling domain event {EventType}", typeof(TEvent).Name);
            }
        }
    }
}
